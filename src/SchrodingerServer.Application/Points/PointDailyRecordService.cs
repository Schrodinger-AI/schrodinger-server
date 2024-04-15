using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Grains.Grain.Points;
using SchrodingerServer.Users.Eto;
using SchrodingerServer.Users.Index;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Points;

public interface IPointDailyRecordService
{
    Task HandlePointDailyChangeAsync(string chainId, string txPointName, HolderDailyChangeDto dto, decimal? symbolPrice);
}

public class PointDailyRecordService : IPointDailyRecordService, ISingletonDependency
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<PointDailyRecordService> _logger;
    private readonly IObjectMapper _objectMapper;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IOptionsMonitor<PointTradeOptions> _pointTradeOptions;

    public PointDailyRecordService(IClusterClient clusterClient, ILogger<PointDailyRecordService> logger,
        IObjectMapper objectMapper, IDistributedEventBus distributedEventBus,
        IOptionsMonitor<PointTradeOptions> pointTradeOptions)
    {
        _clusterClient = clusterClient;
        _logger = logger;
        _objectMapper = objectMapper;
        _distributedEventBus = distributedEventBus;
        _pointTradeOptions = pointTradeOptions;
    }

    /**
     * Support transaction idempotence
     */
    public async Task HandlePointDailyChangeAsync(string chainId, string pointName, HolderDailyChangeDto dto, decimal? symbolPrice)
    {
        AssertHelper.NotNull(dto, "HolderDailyChangeDto is null.");
        if (_pointTradeOptions.CurrentValue.PointMapping.TryGetValue(pointName, out var pointInfo))
        {
            AssertHelper.NotEmpty(pointInfo.ConditionalExp, "PointName {pointName} conditionalExp empty.", pointName);
            AssertHelper.IsTrue(!(pointInfo.NeedMultiplyPrice && symbolPrice == null), "Need multiply symbolPrice but it is null.");
            var match = Regex.Match(dto.Symbol, pointInfo.ConditionalExp);
            if (!match.Success)
            {
                return;
            }
            var pointAmount = CalcPointAmount(dto, pointInfo, symbolPrice);
            var input = new PointDailyRecordGrainDto()
            {
                ChainId = chainId,
                PointName = pointName,
                BizDate = dto.Date,
                Address = dto.Address,
                HolderBalanceId = IdGenerateHelper.GetHolderBalanceId(chainId, dto.Symbol, dto.Address),
                PointAmount = pointAmount
            };
            input.Id = IdGenerateHelper.GetPointDailyRecord(chainId, input.BizDate, input.PointName, input.Address);
            _logger.LogInformation("Handle point daily record id:{id} symbolPrice:{symbolPrice} pointAmount:{pointAmount}", 
                input.Id, symbolPrice, input.PointAmount);
            var pointDailyRecordGrain = _clusterClient.GetGrain<IPointDailyRecordGrain>(input.Id);
            var result = await pointDailyRecordGrain.UpdateAsync(input);
            if (!result.Success)
            {
                _logger.LogError(
                    "Handle Point Daily Record fail, id: {id}.", input.Id);
                throw new UserFriendlyException($"Create Contract Invoke fail, id: {input.Id}.");
            }

            await _distributedEventBus.PublishAsync(
                _objectMapper.Map<PointDailyRecordGrainDto, PointDailyRecordEto>(result.Data));
        }
    }

    private decimal CalcPointAmount(HolderDailyChangeDto dto, PointInfo pointInfo, decimal? symbolPrice)
    {
        //use balance
        if (pointInfo.UseBalance)
        {
            return (decimal)(dto.Balance * pointInfo.Factor * symbolPrice);
        }
        var changeAmount = Math.Abs(dto.ChangeAmount);
        var pointAmount = (decimal)(changeAmount * pointInfo.Factor);

        if (pointInfo.NeedMultiplyPrice && symbolPrice != null)
        {
            return (decimal)(pointAmount * symbolPrice);
        }

        return pointAmount;
    }
}