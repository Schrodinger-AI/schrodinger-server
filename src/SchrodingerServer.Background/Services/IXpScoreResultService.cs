using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Newtonsoft.Json;
using Orleans;
using SchrodingerServer.Background.Dtos;
using SchrodingerServer.Background.Providers;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.ContractInvoke.Index;
using SchrodingerServer.ExceptionHandling;
using SchrodingerServer.Grains.Grain.ZealyScore;
using SchrodingerServer.Grains.Grain.ZealyScore.Dtos;
using SchrodingerServer.Options;
using SchrodingerServer.Zealy;
using SchrodingerServer.Zealy.Eto;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Background.Services;

public interface IXpScoreResultService
{
    Task HandleXpResultAsync();
}

public class XpScoreResultService : IXpScoreResultService, ISingletonDependency
{
    private readonly IZealyProvider _zealyProvider;
    private readonly ILogger<XpScoreResultService> _logger;
    private readonly INESTRepository<ContractInvokeIndex, string> _contractInvokeIndexRepository;
    private readonly UpdateScoreOptions _options;
    private readonly IClusterClient _clusterClient;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IObjectMapper _objectMapper;
    private const string _updateScorePrefix = "UpdateZealyScoreInfo";
    private readonly IDistributedCache<UpdateScoreInfo> _distributedCache;

    public XpScoreResultService(IZealyProvider zealyProvider, ILogger<XpScoreResultService> logger,
        INESTRepository<ContractInvokeIndex, string> contractInvokeIndexRepository,
        IOptionsSnapshot<UpdateScoreOptions> options, IClusterClient clusterClient,
        IDistributedEventBus distributedEventBus, IObjectMapper objectMapper,
        IDistributedCache<UpdateScoreInfo> distributedCache)
    {
        _zealyProvider = zealyProvider;
        _logger = logger;
        _contractInvokeIndexRepository = contractInvokeIndexRepository;
        _clusterClient = clusterClient;
        _distributedEventBus = distributedEventBus;
        _objectMapper = objectMapper;
        _distributedCache = distributedCache;
        _options = options.Value;
    }

    public async Task HandleXpResultAsync()
    {
        var jobIsStart = await CheckJobAsync();
        if (jobIsStart)
        {
            _logger.LogWarning("update zealy score recurring job is started");
            return;
        }

        await HandleXpResultAsync(0, _options.FetchPendingCount);
    }

    private async Task HandleXpResultAsync(int skipCount, int maxResultCount)
    {
        //var startTime = DateTimeOffset.UtcNow.AddDays(-2).ToUnixTimeSeconds();
        var records =
            await _zealyProvider.GetPendingUserXpsAsync(skipCount, maxResultCount, startTime: 0, endTime: 0);
        if (records.IsNullOrEmpty())
        {
            _logger.LogInformation("no pending xp score records");
            return;
        }

        _logger.LogInformation("handle pending xp score records, count:{count}", records.Count);
        var bizIds = records.Select(t => t.BizId).Distinct().ToList();

        var contractInfos = await GetContractInvokeTxByIdsAsync(bizIds);
        foreach (var record in records)
        {
            await HandleRecordAsync(record, contractInfos);
        }

        if (records.Count < maxResultCount)
        {
            return;
        }

        var newSkipCount = skipCount + maxResultCount;
        _logger.LogInformation(
            "handle pending xp score records, skipCount:{skipCount}, maxResultCount:{maxResultCount}", newSkipCount,
            maxResultCount);
        await HandleXpResultAsync(newSkipCount, maxResultCount);
    }
    
    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    private async Task HandleRecordAsync(ZealyUserXpRecordIndex record, List<ContractInvokeIndex> contractInfos)
    {
        var contractInfo = contractInfos.FirstOrDefault(t => t.BizId == record.BizId);
        if (contractInfo == null)
        {
            _logger.LogWarning(
                "modify record status fail, contract info is null, recordId:{recordId}, bizId:{bizId}",
                record.Id, record.BizId ?? "-");
            return;
        }

        // update grain
        await SetFinalStatusAsync(record.Id, contractInfo.Status, record.BizId);
    }

    private async Task SetFinalStatusAsync(string orderId, string status, string bizId, string remark = "")
    {
        var recordGrain = _clusterClient.GetGrain<IXpRecordGrain>(orderId);
        var result = await recordGrain.SetFinalStatusAsync(status, remark);

        if (!result.Success)
        {
            _logger.LogError(
                "upgrade record grain status fail, message:{message}, orderId:{orderId}",
                result.Message, orderId);
            return;
        }

        _logger.LogInformation(
            "modify record status success, recordId:{recordId}, bizId:{bizId}, status:{status}",
            orderId, bizId ?? "-", status);
        var recordEto = _objectMapper.Map<XpRecordGrainDto, XpRecordEto>(result.Data);
        await _distributedEventBus.PublishAsync(recordEto, false, false);
    }

    private async Task<bool> CheckJobAsync()
    {
        var key = $"{_updateScorePrefix}:{DateTime.UtcNow:yyyy-MM-dd}";
        var cache = await _distributedCache.GetAsync(key);
        return cache != null;
    }

    private async Task<List<ContractInvokeIndex>> GetContractInvokeTxByIdsAsync(List<string> bizIds)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<ContractInvokeIndex>, QueryContainer>>
        {
            q => q.Terms(i => i.Field(f => f.BizId).Terms(bizIds))
        };

        QueryContainer Filter(QueryContainerDescriptor<ContractInvokeIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var (totalCount, syncTxs) = await _contractInvokeIndexRepository.GetListAsync(Filter);

        return syncTxs;
    }
}