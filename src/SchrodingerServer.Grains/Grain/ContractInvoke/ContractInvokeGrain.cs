using System.Diagnostics;
using AElf.Client.Dto;
using AElf.Client.Service;
using AElf.ExceptionHandler;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.Common.ApplicationHandler;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Grains.Grain.ApplicationHandler;
using SchrodingerServer.Grains.Grain.Users;
using SchrodingerServer.Grains.State.ContractInvoke;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Grains.Grain.ContractInvoke;

public class ContractInvokeGrain : Grain<ContractInvokeState>, IContractInvokeGrain
{
    private readonly IObjectMapper _objectMapper;
    private readonly IOptionsMonitor<ChainOptions> _chainOptionsMonitor;
    private readonly ILogger<ContractInvokeGrain> _logger;
    private readonly IContractProvider _contractProvider;
    private readonly IBlockchainClientFactory<AElfClient> _blockchainClientFactory;

    public ContractInvokeGrain(IObjectMapper objectMapper, ILogger<ContractInvokeGrain> logger,
        IBlockchainClientFactory<AElfClient> blockchainClientFactory, IContractProvider contractProvider, 
        IOptionsMonitor<ChainOptions> chainOptionsMonitor)
    {
        _objectMapper = objectMapper;
        _logger = logger;
        _blockchainClientFactory = blockchainClientFactory;
        _contractProvider = contractProvider;
        _chainOptionsMonitor = chainOptionsMonitor;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await ReadStateAsync();
        await base.OnActivateAsync(cancellationToken);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        await WriteStateAsync();
        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async Task<GrainResultDto<ContractInvokeGrainDto>> CreateAsync(ContractInvokeGrainDto input)
    {
        if (State.BizId != null && State.BizId.Equals(input.BizId))
        {
            _logger.LogInformation(
                "CreateAsync contract invoke repeated bizId {bizId} ", State.BizId);
            return OfContractInvokeGrainResultDto(true, CommonConstant.TradeRepeated);
        }
        State = _objectMapper.Map<ContractInvokeGrainDto, ContractInvokeState>(input);
        if (State.Id.IsNullOrEmpty())
        {
            State.Id = input.BizId;
        }
        State.Status = ContractInvokeStatus.ToBeCreated.ToString();
        State.CreateTime = DateTime.UtcNow;
        State.UpdateTime = DateTime.UtcNow;

        await WriteStateAsync();
        
        _logger.LogInformation(
            "CreateAsync Contract bizId {bizId} created.", State.BizId);
        
        return OfContractInvokeGrainResultDto(true);
     }

    public async Task<GrainResultDto<ContractInvokeGrainDto>> ExecuteJobAsync(ContractInvokeGrainDto input)
    {
        //State = _objectMapper.Map<ContractInvokeGrainDto, ContractInvokeState>(input);
        //if the data in the grain memory has reached the final state then idempotent return
        if (IsFinalStatus(State.Status))
        {
            return OfContractInvokeGrainResultDto(true);
        }
        
        var status = EnumConverter.ConvertToEnum<ContractInvokeStatus>(State.Status);
        
        var res = await ProcessJob(status);

        if (!res)
        {
            _logger.LogError( "An error occurred during job execution and will be retried bizId:{bizId} txHash: {TxHash} err: {err}",
                State.BizId, State.TransactionId);
        }
        
        return OfContractInvokeGrainResultDto(res);
    }
    
    [ExceptionHandler(typeof(Exception), TargetType = typeof(GrainExceptionHandlingService), MethodName = nameof(GrainExceptionHandlingService.HandleExceptionFalse))]
    private async Task<bool> ProcessJob(ContractInvokeStatus status)
    {
        switch (status)
        {
            case ContractInvokeStatus.ToBeCreated:
                await HandleCreatedAsync();
                break;
            case ContractInvokeStatus.Pending:
                await HandlePendingAsync();
                break;
            case ContractInvokeStatus.Failed:
                await HandleFailedAsync();
                break;
        }

        return true;
    }

    private async Task HandleCreatedAsync()
    {
        //To Generate RawTransaction and Send Transaction
        if (!_chainOptionsMonitor.CurrentValue.ChainInfos.TryGetValue(State.ChainId, out var chainInfo))
        {
            _logger.LogError("ChainOptions chainId:{chainId} has no chain info.", State.ChainId);
            return;
        }

        var rawTxResult = await _contractProvider.CreateTransactionAsync(State.ChainId, chainInfo.PointTxPublicKey,
            State.ContractAddress, State.ContractMethod, State.Param);
        _logger.LogInformation("rawTxResult: {result}",  JsonConvert.SerializeObject(rawTxResult));

        //save txId info
        var oriStatus = State.Status;
        var signedTransaction = rawTxResult.transaction;
        State.TransactionId = signedTransaction.GetHash().ToHex();
        State.RefBlockNumber = signedTransaction.RefBlockNumber;
        State.Sender = signedTransaction.From.ToBase58();
        State.Status = ContractInvokeStatus.Pending.ToString();
        //Send Transaction with catch exception
        await SendTransactionAsync(State.ChainId, signedTransaction);

        _logger.LogInformation(
            "HandleCreatedAsync Contract bizId {bizId} txHash:{txHash} invoke status {oriStatus} to {status}",
            State.BizId, State.TransactionId, oriStatus, State.Status);

        await WriteStateAsync();
    }

    private async Task HandlePendingAsync()
    {
        //To Get Transaction Result
        if (State.TransactionId.IsNullOrEmpty())
        {
            await HandleFailedAsync();
            return;
        }

        var txResult = await _contractProvider.QueryTransactionResultAsync(State.ChainId, State.TransactionId);
        switch (txResult.Status)
        {
            case TransactionState.Mined:
                await HandleSuccessAsync(txResult);
                break;
            case TransactionState.Pending:
                break;
            case TransactionState.Notexisted:
                var client = _blockchainClientFactory.GetClient(State.ChainId);
                var status = await client.GetChainStatusAsync();
                var libHeight = status.LastIrreversibleBlockHeight;
                //check libHeight - refBlockNumber
                if (libHeight - State.RefBlockNumber > _chainOptionsMonitor.CurrentValue.BlockPackingMaxHeightDiff)
                {
                    await UpdateFailedAsync(txResult);
                }

                break;
            default:
                await UpdateFailedAsync(txResult);
                break;
        }
    }

    private async Task HandleFailedAsync()
    {
        //To retry and send HandleCreatedAsync
        if (State.RetryCount >= _chainOptionsMonitor.CurrentValue.MaxRetryCount)
        {
            State.Status = ContractInvokeStatus.FinalFailed.ToString();
        }
        else
        {
            State.Status = ContractInvokeStatus.ToBeCreated.ToString();
            State.RetryCount += 1;
        }
        _logger.LogInformation(
            "HandleFailedAsync Contract bizId {bizId} txHash:{txHash} invoke status to {status}, retryCount:{retryCount}",
            State.BizId, State.TransactionId, State.Status, State.RetryCount);
        await WriteStateAsync();
    }
    
    
    private async Task HandleSuccessAsync(TransactionResultDto txResult)
    {
        var oriStatus = State.Status;
        State.BlockHeight = txResult.BlockNumber;
        State.Status = ContractInvokeStatus.Success.ToString();
        _logger.LogInformation(
            "HandlePendingAsync Contract bizId {bizId} txHash:{txHash} invoke status {oriStatus} to {status}",
            State.BizId, State.TransactionId, oriStatus, State.Status);
        await WriteStateAsync();
    }
    
    private async Task UpdateFailedAsync(TransactionResultDto txResult)
    {
        var oriStatus = State.Status;
        State.Status = ContractInvokeStatus.Failed.ToString();
        State.TransactionStatus = txResult.Status;
        // When Transaction status is not mined or pending, Transaction is judged to be failed.
        State.Message = $"Transaction failed, status: {State.Status}. error: {txResult.Error}";
        _logger.LogWarning(
            "TransactionFailedAsync Contract bizId {bizId} txHash:{txHash} invoke status {oriStatus} to {status}",
            State.BizId, State.TransactionId, oriStatus, State.Status);
        
        await WriteStateAsync();
    }
    
    private async Task SendTransactionAsync(string chainId, Transaction signedTransaction)
    {
        await _contractProvider.SendTransactionAsync(chainId, signedTransaction);
    }
    
    private GrainResultDto<ContractInvokeGrainDto> OfContractInvokeGrainResultDto(bool success, string message = null)
    {
        return new GrainResultDto<ContractInvokeGrainDto>()
        {
            Data = _objectMapper.Map<ContractInvokeState, ContractInvokeGrainDto>(State),
            Success = success,
            Message = message
        };
    }

    private bool IsFinalStatus(string status)
    {
        return status.Equals(ContractInvokeStatus.Success.ToString())
               || status.Equals(ContractInvokeStatus.FinalFailed.ToString());
    }
}