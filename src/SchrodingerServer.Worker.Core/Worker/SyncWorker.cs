using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.Grains.Grain.Synchronize;
using SchrodingerServer.Worker.Core.Options;
using SchrodingerServer.Worker.Core.Provider;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace SchrodingerServer.Worker.Core.Worker;

public class SyncWorker : AsyncPeriodicBackgroundWorkerBase
{
    private long _latestSubscribeHeight;
    private readonly ILogger<SyncWorker> _logger;
    private readonly IClusterClient _clusterClient;
    private readonly IIndexerProvider _indexerProvider;
    private readonly ConcurrentQueue<string> _finishedQueue;
    private readonly IOptionsMonitor<WorkerOptions> _options;

    public SyncWorker(AbpAsyncTimer timer, IOptionsMonitor<WorkerOptions> workerOptions, ILogger<SyncWorker> logger,
        IServiceScopeFactory serviceScopeFactory, IIndexerProvider indexerProvider, IClusterClient clusterClient) :
        base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = workerOptions;
        _clusterClient = clusterClient;
        _indexerProvider = indexerProvider;
        _finishedQueue = new();
        Timer.Period = 1000 * _options.CurrentValue.SearchTimer;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        await ExecuteSearchAsync();
        // Query and execution need to ensure serialization
        await ExecuteSyncAsync();
    }

    private async Task ExecuteSearchAsync()
    {
        if (_latestSubscribeHeight == 0) await SearchWorkerInitializing();

        var chainId = _options.CurrentValue.SyncSourceChainId;
        var blockLatestHeight = await _indexerProvider.GetIndexBlockHeightAsync(chainId);
        if (blockLatestHeight <= _latestSubscribeHeight)
        {
            _logger.LogDebug("[Search] {chain} confirmed height hasn't been updated yet, will try later.", chainId);
            return;
        }

        var batchSize = _options.CurrentValue.BackFillBatchSize;
        var jobs = new List<string>();

        for (var from = _latestSubscribeHeight + 1; from <= blockLatestHeight; from += batchSize)
        {
            _logger.LogDebug("[Search] Next search window start {from}", from);
            var confirms = await _indexerProvider.SubscribeConfirmedAsync(chainId,
                Math.Min(from + batchSize - 1, blockLatestHeight), from);
            confirms = confirms.Where(s => !string.IsNullOrEmpty(s)).ToList();
            if (confirms.Count > 0) jobs.AddRange(confirms);
        }

        if (jobs.Count > 0) await AddOrUpdateConfirmedEventsAsync(jobs);

        await UpdateSubscribeHeightAsync(blockLatestHeight);
    }

    private async Task ExecuteSyncAsync()
    {
        var grainClient = _clusterClient.GetGrain<ISyncPendingGrain>(GenerateSyncPendingListGrainId());

        await Task.WhenAll(ScheduleTasks(await grainClient.GetSyncPendingListAsync())
            .Select(HandlerJobExecuteAsync));

        if (!_finishedQueue.IsEmpty)
        {
            var finished = _finishedQueue.ToList();
            _logger.LogInformation("[Execute] Finished tx count {count} list: {list} ", finished.Count,
                string.Join(", ", finished));
            await grainClient.DeleteSyncPendingList(finished);
            _finishedQueue.Clear();
        }
    }

    private async Task HandlerJobExecuteAsync(string transactionId)
    {
        try
        {
            _logger.LogDebug("[Execute] Start sync transaction {tx}", transactionId);
            var jobGrain = _clusterClient.GetGrain<ISyncGrain>(transactionId);
            var result = await jobGrain.ExecuteJobAsync(new SyncJobGrainDto { Id = transactionId });
            if (result.Data?.Status == SyncJobStatus.CrossChainTokenCreated)
            {
                _logger.LogInformation("[Execute] Transaction {tx} token sync finished.", transactionId);
                _finishedQueue.Enqueue(result.Data.TransactionId);
            }
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Execute job {tx} timeout.", transactionId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Execute job {tx} failed.", transactionId);
        }
    }

    private async Task UpdateSubscribeHeightAsync(long height)
    {
        _latestSubscribeHeight = height;
        await _clusterClient.GetGrain<ISubscribeGrain>(GenerateSubscribeHeightGrainId())
            .SetSubscribeHeightAsync(height);
    }

    private async Task AddOrUpdateConfirmedEventsAsync(List<string> events)
        => await _clusterClient.GetGrain<ISyncPendingGrain>(GenerateSyncPendingListGrainId())
            .AddOrUpdateSyncPendingList(events);

    private async Task SearchWorkerInitializing()
    {
        var grainHeight = await _clusterClient.GetGrain<ISubscribeGrain>(GenerateSubscribeHeightGrainId())
            .GetSubscribeHeightAsync();
        _latestSubscribeHeight = grainHeight == 0 ? _options.CurrentValue.SubscribeStartHeight : grainHeight;
    }

    private List<string> ScheduleTasks(List<string> sourceList)
    {
        _logger.LogInformation("[Execute] There are a total of {count} tasks to be executed", sourceList.Count);

        if (sourceList.Count <= _options.CurrentValue.MaximumNumberPerTask) return sourceList;

        //  Execution Accelerate
        var targetLength = sourceList.Count;
        if (targetLength > _options.CurrentValue.SyncExecutionAccelerationThreshold)
        {
            targetLength = _options.CurrentValue.SyncExecutionAccelerationThreshold;
            _logger.LogWarning("Due to task blocking, the number of executed tasks will be increased to {amount}.",
                targetLength);
        }
        else
        {
            targetLength = _options.CurrentValue.MaximumNumberPerTask;
            _logger.LogWarning(
                "In order to prevent excessive execution pressure from causing the sync tasks to fail. the number is reduced to {amount} at a time.",
                targetLength);
        }

        var resultList = new List<string>();
        var tempList = new List<string>(sourceList);
        for (var i = 0; i < targetLength; i++)
        {
            var index = new Random().Next(0, tempList.Count);
            resultList.Add(tempList[index]);
            tempList.RemoveAt(index);
        }

        return resultList;
    }

    private string GenerateSubscribeHeightGrainId() =>
        GuidHelper.UniqGuid(_options.CurrentValue.SubscribeStartHeightGrainId).ToString();

    private string GenerateSyncPendingListGrainId() =>
        GuidHelper.UniqGuid(_options.CurrentValue.SyncPendingListGrainId).ToString();
}