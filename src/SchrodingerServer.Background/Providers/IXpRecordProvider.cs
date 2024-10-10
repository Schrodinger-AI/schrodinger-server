using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Grains.Grain.ZealyScore;
using SchrodingerServer.Grains.Grain.ZealyScore.Dtos;
using SchrodingerServer.Options;
using SchrodingerServer.Points;
using SchrodingerServer.Users.Dto;
using SchrodingerServer.Zealy.Eto;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Background.Providers;

public interface IXpRecordProvider
{
    Task CreateRecordAsync(string userId, string address, decimal currentXp, decimal xp);
    Task SetStatusToPendingAsync(PointSettleDto pointSettleDto);
}

public class XpRecordProvider : IXpRecordProvider, ISingletonDependency
{
    private readonly ZealyScoreOptions _options;
    private readonly ILogger<XpRecordProvider> _logger;
    private readonly IClusterClient _clusterClient;
    private readonly IObjectMapper _objectMapper;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IPointSettleService _pointSettleService;

    public XpRecordProvider(
        IOptionsSnapshot<ZealyScoreOptions> options,
        ILogger<XpRecordProvider> logger,
        IClusterClient clusterClient, IObjectMapper objectMapper, IDistributedEventBus distributedEventBus,
        IPointSettleService pointSettleService)
    {
        _logger = logger;
        _clusterClient = clusterClient;
        _objectMapper = objectMapper;
        _distributedEventBus = distributedEventBus;
        _pointSettleService = pointSettleService;
        _options = options.Value;
    }

    [AutomaticRetry(Attempts = 20, DelaysInSeconds = new[] { 40 })]
    public async Task CreateRecordAsync(string userId, string address, decimal currentXp, decimal xp)
    {
        var recordId = $"{userId}-{DateTime.UtcNow:yyyy-MM-dd}";
        _logger.LogInformation("begin create, recordId:{recordId}", recordId);

        var recordDto = new XpRecordGrainDto
        {
            Id = recordId,
            IncreaseXp = xp,
            CurrentXp = currentXp,
            PointsAmount = DecimalHelper.MultiplyByPowerOfTen(xp * _options.Coefficient, 8),
            BizId = string.Empty,
            Status = ContractInvokeStatus.ToBeCreated.ToString(),
            UserId = userId,
            Address = address
        };

        var recordGrain = _clusterClient.GetGrain<IXpRecordGrain>(recordId);
        var result = await recordGrain.CreateAsync(recordDto);

        // clear recordInfos
        BackgroundJob.Enqueue(() => HandleRecordInfosAsync(userId));

        if (!result.Success)
        {
            _logger.LogError(
                "add record grain fail, message:{message}, userId:{userId}, address:{address}, xp:{xp}",
                result.Message, userId, address, xp);
            return;
        }

        var recordEto = _objectMapper.Map<XpRecordGrainDto, XpRecordEto>(result.Data);
        await _distributedEventBus.PublishAsync(recordEto, false, false);
        _logger.LogInformation("end create record, recordId:{recordId}", recordId);
    }

    [AutomaticRetry(Attempts = 20, DelaysInSeconds = new[] { 40 })]
    public async Task SetStatusToPendingAsync(PointSettleDto pointSettleDto)
    {
        await _pointSettleService.BatchSettleAsync(pointSettleDto);
    }

    public async Task HandleRecordInfosAsync(string userId)
    {
        var userGrain = _clusterClient.GetGrain<IZealyUserXpGrain>(userId);
        var userDto = await userGrain.GetUserXpInfoAsync();

        if (!userDto.Success)
        {
            _logger.LogError("get user xp info error, userId:{userId}", userId);
            return;
        }

        var recordInfos = userDto.Data.RecordInfos;
        if (recordInfos.IsNullOrEmpty())
        {
            return;
        }

        // grain
        foreach (var recordInfo in recordInfos)
        {
            var recordId = $"{userId}-{recordInfo.Date}";
            var recordGrain = _clusterClient.GetGrain<IXpRecordGrain>(recordId);
            var result = await recordGrain.HandleRecordAsync(recordInfo, userDto.Data.Id, userDto.Data.Address);
            if (!result.Success)
            {
                _logger.LogError(
                    "handle record info fail, message:{message}, userId:{userId}, recordInfo:{recordInfo}",
                    result.Message, userId, JsonConvert.SerializeObject(recordInfo));
                return;
            }

            var recordEto = _objectMapper.Map<XpRecordGrainDto, AddXpRecordEto>(result.Data);
            await _distributedEventBus.PublishAsync(recordEto, false, false);
            _logger.LogInformation("handle record info success, recordId:{recordId}", recordId);
        }
    }
}