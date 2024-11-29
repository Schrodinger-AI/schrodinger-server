using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AElf.Types;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Schrodinger;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.ContractInvoke.Eto;
using SchrodingerServer.ExceptionHandling;
using SchrodingerServer.Grains.Grain;
using SchrodingerServer.Grains.Grain.ContractInvoke;
using SchrodingerServer.Users.Dto;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;
using UserPoints = Schrodinger.UserPoints;

namespace SchrodingerServer.Points;

public interface IPointSettleService
{
    Task BatchSettleAsync(PointSettleDto dto);
}

public class PointSettleService : IPointSettleService, ISingletonDependency
{
    private readonly ILogger<PointSettleService> _logger;
    private readonly IClusterClient _clusterClient;
    private readonly IOptionsMonitor<PointTradeOptions> _pointTradeOptions;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IObjectMapper _objectMapper;

    public PointSettleService(ILogger<PointSettleService> logger, IClusterClient clusterClient,
        IOptionsMonitor<PointTradeOptions> pointTradeOptions, IDistributedEventBus distributedEventBus,
        IObjectMapper objectMapper)
    {
        _logger = logger;
        _clusterClient = clusterClient;
        _pointTradeOptions = pointTradeOptions;
        _distributedEventBus = distributedEventBus;
        _objectMapper = objectMapper;
    }

    public async Task BatchSettleAsync(PointSettleDto dto)
    {
        _logger.LogInformation("BatchSettle begin, bizId:{bizId}.", dto.BizId);
        AssertHelper.NotEmpty(dto.BizId, "Invalid bizId.");
        _logger.LogInformation("BatchSettle bizId:{bizId}", dto.BizId);
        var actionName = _pointTradeOptions.CurrentValue.GetActionName(dto.PointName);
        AssertHelper.NotEmpty(actionName, "Invalid actionName.");
        var chainInfo = _pointTradeOptions.CurrentValue.GetChainInfo(dto.ChainId);
        AssertHelper.NotNull(chainInfo, "Invalid chainInfo.");
        var userPoints = dto.UserPointsInfos
            .Where(item => item.PointAmount > 0)
            .Select(item => new UserPoints
            {
                UserAddress = Address.FromBase58(item.Address),
                UserPointsValue = DecimalHelper.ConvertBigInteger(item.PointAmount, 0)
            }).ToList();
        var batchSettleInput = new BatchSettleInput()
        {
            ActionName = actionName,
            UserPointsList = { userPoints }
        };
        var input = new ContractInvokeGrainDto
        {
            ChainId = dto.ChainId,
            BizId = dto.BizId,
            BizType = dto.PointName,
            ContractAddress = chainInfo.SchrodingerContractAddress,
            ContractMethod = chainInfo.ContractMethod,
            Param = batchSettleInput.ToByteString().ToBase64()
            //ParamJson = JsonConvert.SerializeObject(dto)
        };
        var contractInvokeGrain = _clusterClient.GetGrain<IContractInvokeGrain>(dto.BizId);
        _logger.LogInformation("BatchSettle CreateAsync, bizId:{bizId}.", dto.BizId);
        var result = await contractInvokeGrain.CreateAsync(input);
        if (!result.Success)
        {
            _logger.LogError(
                "Create Contract Invoke fail, bizId: {dto.BizId}.", dto.BizId);
            throw new UserFriendlyException($"Create Contract Invoke fail, bizId: {dto.BizId}.");
        }
        
        _logger.LogInformation("BatchSettle success, bizId:{bizId}.", dto.BizId);
        await PublishData(
            _objectMapper.Map<ContractInvokeGrainDto, ContractInvokeEto>(result.Data));
    }
    
    [ExceptionHandler(typeof(Exception), Message = "BatchSettle PublishAsync error", TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    private async Task PublishData(ContractInvokeEto data)
    {
        await _distributedEventBus.PublishAsync(data);
    }
}