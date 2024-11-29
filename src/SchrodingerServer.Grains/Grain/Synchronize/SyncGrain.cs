using AElf;
using AElf.Client.Dto;
using AElf.Contracts.MultiToken;
using AElf.ExceptionHandler;
using AElf.Types;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.Grains.Grain.ApplicationHandler;
using SchrodingerServer.Grains.Grain.Users;
using SchrodingerServer.Grains.State.Sync;
using Volo.Abp.ObjectMapping;
using IContractProvider = SchrodingerServer.Grains.Grain.Provider.IContractProvider;

namespace SchrodingerServer.Grains.Grain.Synchronize;

public class SyncGrain : Grain<SyncState>, ISyncGrain
{
    private readonly string _targetChainId;
    private readonly string _sourceChainId;
    private readonly ILogger<SyncGrain> _logger;
    private readonly IObjectMapper _objectMapper;
    private readonly IContractProvider _contractProvider;
    private readonly IOptionsMonitor<SyncTokenOptions> _syncOptions;

    public SyncGrain(ILogger<SyncGrain> logger, IContractProvider contractProvider,
        IOptionsMonitor<SyncTokenOptions> syncOptions, IObjectMapper objectMapper)
    {
        _logger = logger;
        _syncOptions = syncOptions;
        _objectMapper = objectMapper;
        _contractProvider = contractProvider;
        _targetChainId = _syncOptions.CurrentValue.TargetChainId;
        _sourceChainId = _syncOptions.CurrentValue.SourceChainId;
    }
    
    
    public async Task<GrainResultDto<SyncGrainDto>> ExecuteJobAsync(SyncJobGrainDto input)
    {
        if (string.IsNullOrEmpty(State.Id))
        {
            State.Id = input.Id;
            State.Status = SyncJobStatus.TokenCreating;
        }
        else
        {
            _logger.LogDebug("{txId} status: {status} now", State.TransactionId, State.Status);
        }

        var result = new GrainResultDto<SyncGrainDto>();
        State.TransactionId = input.Id;
        await WriteStateAsync();

        // try
        // {
        //     switch (State.Status)
        //     {
        //         case SyncJobStatus.TokenCreating:
        //             await HandleTokenCreatingAsync();
        //             break;
        //         case SyncJobStatus.TokenValidating:
        //             await HandleTokenValidatingAsync();
        //             break;
        //         case SyncJobStatus.WaitingIndexing:
        //             await HandleMainChainIndexSideChainAsync();
        //             break;
        //         case SyncJobStatus.WaitingSideIndexing:
        //             await HandleWaitingIndexingAsync();
        //             break;
        //         case SyncJobStatus.CrossChainTokenCreating:
        //             await HandleCrossChainTokenCreatingAsync();
        //             break;
        //         case SyncJobStatus.CrossChainTokenCreated:
        //             break;
        //         default:
        //             throw new InvalidOperationException("Invalid status");
        //     }
        //
        //     result.Data = _objectMapper.Map<SyncState, SyncGrainDto>(State);
        // }
        // catch (AElf.Client.AElfClientException ce)
        // {
        //     _logger.LogError(ce, "When sync task {tx}, the call to sdk failed. Will try again later.", input.Id);
        // }
        // catch (Exception e)
        // {
        //     _logger.LogError(e, "Sync job {tx} Failed, Synchronization will restart", input.Id);
        //     await Resync(input.Id);
        // }

        var res = await ProcessJob();
        if (res == 1)
        {
            _logger.LogError( "Sync job {tx} Failed, Synchronization will restart", input.Id);
            await Resync(input.Id);
        } 
        else if (res == 0)
        {
            result.Data = _objectMapper.Map<SyncState, SyncGrainDto>(State);
        }
        
        return result;
    }
    
    [ExceptionHandler(typeof(AElf.Client.AElfClientException), Message = "When sync task, the call to sdk failed. Will try again later", TargetType = typeof(GrainExceptionHandlingService), MethodName = nameof(GrainExceptionHandlingService.HandleAElfClientException))]
    [ExceptionHandler(typeof(Exception), Message = "Sync job Failed, Synchronization will restart", TargetType = typeof(GrainExceptionHandlingService), MethodName = nameof(GrainExceptionHandlingService.HandleException))]
    private async Task<int> ProcessJob()
    {
        switch (State.Status)
        {
            case SyncJobStatus.TokenCreating:
                await HandleTokenCreatingAsync();
                break;
            case SyncJobStatus.TokenValidating:
                await HandleTokenValidatingAsync();
                break;
            case SyncJobStatus.WaitingIndexing:
                await HandleMainChainIndexSideChainAsync();
                break;
            case SyncJobStatus.WaitingSideIndexing:
                await HandleWaitingIndexingAsync();
                break;
            case SyncJobStatus.CrossChainTokenCreating:
                await HandleCrossChainTokenCreatingAsync();
                break;
            case SyncJobStatus.CrossChainTokenCreated:
                break;
            default:
                throw new InvalidOperationException("Invalid status");
        }

        return 0;
    }

    # region Token crossChain

    private async Task HandleTokenCreatingAsync()
    {
        var tokenCreated = _contractProvider.ParseLogEvents<TokenCreated>(
            await _contractProvider.GetTxResultAsync(_sourceChainId, State.TransactionId));
        if (tokenCreated == null || string.IsNullOrEmpty(tokenCreated.Symbol))
        {
            _logger.LogWarning("Transaction {tx} don't have TokenCreated event, please check! ", State.TransactionId);
            State.Status = SyncJobStatus.CrossChainTokenCreated;
            await WriteStateAsync();
            return;
        }

        var tokenSymbol = tokenCreated.Symbol;
        var tokenInfo = await _contractProvider.GetTokenInfoAsync(_sourceChainId, tokenSymbol);

        // check token is cross chain created
        if (await CheckTokenExistAsync(tokenSymbol))
        {
            _logger.LogWarning("Symbol {symbol} is CrossChain to target chain. ", tokenSymbol);
            State.Status = SyncJobStatus.CrossChainTokenCreated;
            await WriteStateAsync();
            return;
        }

        State.Symbol = tokenInfo.Symbol;
        (State.ValidateTokenTx, State.ValidateTokenTxId) =
            await _contractProvider.SendValidateTokenExistAsync(_sourceChainId, tokenInfo);
        State.Status = SyncJobStatus.TokenValidating;
        _logger.LogInformation("TransactionId {txId} update status to {status} in HandleTokenCreatingAsync.",
            State.TransactionId, State.Status);

        await WriteStateAsync();
    }

    private async Task HandleTokenValidatingAsync()
    {
        var txResult = await _contractProvider.GetTxResultAsync(_sourceChainId, State.ValidateTokenTxId);
        if (!await CheckTxStatusAsync(txResult))
        {
            _logger.LogWarning("Validate token exist transaction not ready now.");
            return;
        }

        if (txResult.BlockNumber == 0) return;

        State.ValidateTokenHeight = txResult.BlockNumber;
        State.Status = SyncJobStatus.WaitingIndexing;

        _logger.LogInformation("TxHash id {txHash} update status to {status} in HandleTokenValidatingAsync.",
            State.TransactionId, State.Status);

        await WriteStateAsync();
    }


    private async Task HandleMainChainIndexSideChainAsync()
    {
        // Check MainChain Index SideChain
        // First, the main chain must index to the transaction height of the side chain.
        var indexHeight = await GetSideChainIndexHeightAsync();
        if (indexHeight < State.ValidateTokenHeight)
        {
            _logger.LogInformation(
                "[Synchronize] Block is not recorded, now index height {indexHeight}, expected height:{ValidateHeight}",
                indexHeight, State.ValidateTokenHeight);
            return;
        }

        // Then record the number of main chain heights. Only the side chain bidirectional index can continue to cross the chain.
        State.MainChainIndexHeight = await _contractProvider.GetBlockLatestHeightAsync(_targetChainId);
        State.Status = SyncJobStatus.WaitingSideIndexing;

        _logger.LogInformation("TxHash id {txHash} update status to {status} in HandleMainChainIndexSideChainAsync.",
            State.TransactionId, State.Status);

        await WriteStateAsync();
    }

    private async Task HandleWaitingIndexingAsync()
    {
        var indexHeight = await GetMainChainIndexHeightAsync();
        if (indexHeight < State.MainChainIndexHeight)
        {
            _logger.LogInformation(
                "[Synchronize] The height of the main chain has not been indexed yet, now index height {indexHeight}, expected height:{mainHeight}",
                indexHeight, State.MainChainIndexHeight);
            return;
        }

        // check token is cross chain created
        if (await CheckTokenExistAsync(State.Symbol))
        {
            _logger.LogWarning("Symbol {symbol} is CrossChain to target chain, no need create again. ", State.Symbol);
            State.Status = SyncJobStatus.CrossChainTokenCreated;
            await WriteStateAsync();
            return;
        }

        var merklePath = await _contractProvider.GetMerklePathAsync(_sourceChainId, State.ValidateTokenTxId);
        if (merklePath == null) return;

        var crossChainMerkleProof =
            await _contractProvider.GetCrossChainMerkleProofContextAsync(_sourceChainId, State.ValidateTokenHeight);

        var createTokenParams = new CrossChainCreateTokenInput
        {
            FromChainId = ChainHelper.ConvertBase58ToChainId(_sourceChainId),
            ParentChainHeight = crossChainMerkleProof.BoundParentChainHeight,
            TransactionBytes = ByteString.CopyFrom(ByteArrayHelper.HexStringToByteArray(State.ValidateTokenTx)),
            MerklePath = new MerklePath()
        };

        foreach (var node in merklePath.MerklePathNodes)
        {
            createTokenParams.MerklePath.MerklePathNodes.Add(new MerklePathNode
            {
                Hash = new Hash { Value = Hash.LoadFromHex(node.Hash).Value },
                IsLeftChildNode = node.IsLeftChildNode
            });
        }

        foreach (var node in crossChainMerkleProof.MerklePathFromParentChain.MerklePathNodes)
        {
            createTokenParams.MerklePath.MerklePathNodes.Add(new MerklePathNode
            {
                Hash = new Hash { Value = node.Hash.Value },
                IsLeftChildNode = node.IsLeftChildNode
            });
        }

        var txId = await _contractProvider.CrossChainCreateToken(_targetChainId, createTokenParams);
        _logger.LogInformation("CrossChainCreateTokenTxId {TxId}", txId);

        State.CrossChainCreateTokenTxId = txId;
        State.Status = SyncJobStatus.CrossChainTokenCreating;

        _logger.LogInformation("TxHash id {txHash} update status to {status} in HandleWaitingIndexingAsync.",
            State.TransactionId, State.Status);

        await WriteStateAsync();
    }

    private async Task HandleCrossChainTokenCreatingAsync()
    {
        var txResult = await _contractProvider.GetTxResultAsync(_targetChainId, State.CrossChainCreateTokenTxId);
        if (!await CheckTxStatusAsync(txResult))
        {
            _logger.LogWarning("Cross chain create token transaction not ready now.");
            return;
        }

        State.Status = SyncJobStatus.CrossChainTokenCreated;

        _logger.LogInformation("TxHash id {txHash} update status to {status} in HandleCrossChainTokenCreatingAsync.",
            State.TransactionId, State.Status);

        await WriteStateAsync();
    }


    private async Task<bool> CheckTxStatusAsync(TransactionResultDto txResult)
    {
        switch (txResult.Status)
        {
            case TransactionState.Mined:
                _logger.LogInformation("Transaction {tx} is mined.", txResult.TransactionId);
                return true;
            case TransactionState.Pending:
                _logger.LogWarning("Transaction {tx} is pending.", txResult.TransactionId);
                return false;
            default:
                _logger.LogError("Transaction failed, TxHash id {txHash} update status to failed, error message {msg}.",
                    State.TransactionId, txResult.Error);
                throw new Exception($"Transaction failed, status: {State.Status}. error: {txResult.Error}");
        }
    }

    private async Task<bool> CheckTokenExistAsync(string tokenSymbol)
        => (await _contractProvider.GetTokenInfoAsync(_targetChainId, tokenSymbol)).Symbol == tokenSymbol;

    private async Task Resync(string tx)
    {
        State = new SyncState { Id = tx, TransactionId = tx, Status = SyncJobStatus.TokenCreating };
        await WriteStateAsync();
    }

    private async Task<long> GetSideChainIndexHeightAsync()
    {
        var indexHeight = await GrainFactory.GetGrain<IIndexBlockHeightGrain>(GuidHelper
            .UniqGuid(_syncOptions.CurrentValue.IndexBlockHeightGrainId).ToString()).GetSideChainIndexHeightAsync();

        _logger.LogDebug("Now SideChain index height {indexHeight} from grain", indexHeight);

        return indexHeight == 0
            ? await _contractProvider.GetSideChainIndexHeightAsync(_targetChainId, _sourceChainId)
            : indexHeight;
    }

    private async Task<long> GetMainChainIndexHeightAsync()
    {
        var indexHeight = await GrainFactory.GetGrain<IIndexBlockHeightGrain>(GuidHelper
            .UniqGuid(_syncOptions.CurrentValue.IndexBlockHeightGrainId).ToString()).GetMainChainIndexHeightAsync();

        _logger.LogDebug("Now MainChain index height {indexHeight} from grain", indexHeight);

        return indexHeight == 0 ? await _contractProvider.GetIndexHeightAsync(_sourceChainId) : indexHeight;
    }

    #endregion
}