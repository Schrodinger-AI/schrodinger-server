using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.Grains.Grain.Synchronize;
using SchrodingerServer.Worker.Core.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace SchrodingerServer.Worker.Core.Worker;

public class IndexBlockHeightWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<IndexBlockHeightWorker> _logger;
    private readonly IOptionsMonitor<IndexBlockHeightOptions> _options;

    public IndexBlockHeightWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsMonitor<IndexBlockHeightOptions> options, ILogger<IndexBlockHeightWorker> logger,
        IClusterClient clusterClient) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _options = options;
        _clusterClient = clusterClient;
        Timer.Period = 1000 * _options.CurrentValue.SearchTimer;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogDebug("[IndexBlockHeightWorker] Start to update index block height in grain.");
        var grainClient = _clusterClient.GetGrain<IIndexBlockHeightGrain>(GuidHelper
            .UniqGuid(_options.CurrentValue.IndexBlockHeightGrainId).ToString());
        var updateSideChainIndexHeightResult = await grainClient.UpdateSideChainIndexHeightAsync(
            _options.CurrentValue.TargetChainId, _options.CurrentValue.SourceChainId);

        _logger.LogInformation("[IndexBlockHeightWorker] Update SideChain {chainId} IndexHeight in grain to {height}",
            _options.CurrentValue.TargetChainId, updateSideChainIndexHeightResult);

        var updateMainChainIndexHeightResult =
            await grainClient.UpdateMainChainIndexHeightAsync(_options.CurrentValue.SourceChainId);

        _logger.LogInformation("[IndexBlockHeightWorker] Update MainChain {chainId} IndexHeight in grain to {height}",
            _options.CurrentValue.SourceChainId, updateMainChainIndexHeightResult);
    }
}