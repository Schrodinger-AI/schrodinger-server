using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using SchrodingerServer.Background.Providers;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.ExceptionHandling;
using SchrodingerServer.Grains.Grain.ZealyScore;
using SchrodingerServer.Grains.Grain.ZealyScore.Dtos;
using SchrodingerServer.Options;
using SchrodingerServer.Users.Dto;
using SchrodingerServer.Zealy;
using SchrodingerServer.Zealy.Eto;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Background.Services;

public interface IXpScoreSettleService
{
    Task BatchSettleAsync();
}

public class XpScoreSettleService : IXpScoreSettleService, ISingletonDependency
{
    private readonly ILogger<XpScoreSettleService> _logger;
    private readonly IZealyUserXpRecordProvider _recordProvider;
    private readonly ZealyScoreOptions _options;
    private readonly UpdateScoreOptions _updateScoreOptions;
    private readonly IClusterClient _clusterClient;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IObjectMapper _objectMapper;
    private readonly IXpRecordProvider _xpRecordProvider;

    public XpScoreSettleService(ILogger<XpScoreSettleService> logger, IOptionsSnapshot<ZealyScoreOptions> options,
        IZealyUserXpRecordProvider recordProvider, IOptionsSnapshot<UpdateScoreOptions> updateScoreOptions,
        IClusterClient clusterClient, IDistributedEventBus distributedEventBus, IObjectMapper objectMapper,
        IXpRecordProvider xpRecordProvider)
    {
        _logger = logger;
        _recordProvider = recordProvider;
        _clusterClient = clusterClient;
        _distributedEventBus = distributedEventBus;
        _objectMapper = objectMapper;
        _xpRecordProvider = xpRecordProvider;
        _updateScoreOptions = updateScoreOptions.Value;
        _options = options.Value;
    }

    public async Task BatchSettleAsync()
    {
        var records = await _recordProvider.GetToCreateRecordAsync(0, _updateScoreOptions.FetchSettleCount);
        if (records.IsNullOrEmpty())
        {
            _logger.LogInformation("No record to settle");
            return;
        }

        _logger.LogInformation("need to settle records count:{count}", records.Count);
        await RecordBatchSettleAsync(records);
    }

    private async Task RecordBatchSettleAsync(List<ZealyUserXpRecordIndex> records)
    {
        var recurCount = (records.Count / _updateScoreOptions.SettleCount) + 1;
        for (var i = 0; i < recurCount; i++)
        {
            var bizId = $"{Guid.NewGuid().ToString()}-{DateTime.UtcNow:yyyy-MM-dd}";
            var skipCount = _updateScoreOptions.SettleCount * i;
            var settleRecords = records.Skip(skipCount).Take(_updateScoreOptions.SettleCount).ToList();

            if (settleRecords.IsNullOrEmpty()) return;
            await BatchSettleAsync(bizId, settleRecords);
        }
    }

    private async Task BatchSettleAsync(string bizId, List<ZealyUserXpRecordIndex> records)
    {
        var pointSettleDto = new PointSettleDto()
        {
            ChainId = _options.ChainId,
            BizId = bizId,
            PointName = _options.PointName
        };

        var pointRecords = await GetRecordsAsync(records, bizId);
        if (pointRecords.IsNullOrEmpty())
        {
            return;
        }

        var points = pointRecords.Select(record => new UserPointInfo()
            { Address = record.Address, PointAmount = record.PointsAmount }).ToList();

        pointSettleDto.UserPointsInfos = points;

        BackgroundJob.Enqueue(() => _xpRecordProvider.SetStatusToPendingAsync(pointSettleDto));
        _logger.LogInformation("BatchSettle finish, bizId:{bizId}", bizId);
    }

    private async Task<List<ZealyUserXpRecordIndex>> GetRecordsAsync(List<ZealyUserXpRecordIndex> records, string bizId)
    {
        var pointRecords = new List<ZealyUserXpRecordIndex>();

        foreach (var record in records)
        {
            await ProcessRecord(record, bizId);
            pointRecords.Add(record);
        }

        return pointRecords;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    private async Task ProcessRecord(ZealyUserXpRecordIndex record, string bizId)
    {
        var recordGrain = _clusterClient.GetGrain<IXpRecordGrain>(record.Id);
        var result = await recordGrain.GetAsync();

        if (!result.Success)
        {
            _logger.LogError(
                "get record grain fail, message:{message}, recordId:{recordId}",
                result.Message, record.Id);
            return;
        }

        if (result.Data.Status != ContractInvokeStatus.ToBeCreated.ToString())
        {
            await _distributedEventBus.PublishAsync(
                _objectMapper.Map<XpRecordGrainDto, XpRecordEto>(result.Data), false, false);
            _logger.LogWarning("record already handled, recordId:{recordId}", record.Id);
            return;
        }

        var updateResult = await recordGrain.SetStatusToPendingAsync(bizId);
        if (!updateResult.Success)
        {
            _logger.LogError(
                "update record grain status fail, message:{message}, recordId:{recordId}",
                updateResult.Message, record.Id);
            return;
        }

        _logger.LogInformation("settle record, recordId:{recordId}", record.Id);
        var recordEto = _objectMapper.Map<XpRecordGrainDto, XpRecordEto>(updateResult.Data);
        await _distributedEventBus.PublishAsync(recordEto, false, false);
    }
}