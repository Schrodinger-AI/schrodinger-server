using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchrodingerServer.EntityEventHandler.Core.Options;
using SchrodingerServer.Points;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Threading;

namespace SchrodingerServer.EntityEventHandler.Core.Worker;

public class PointSendTransactionWorker: AsyncPeriodicBackgroundWorkerBase
{
    private readonly IPointAssemblyTransactionService _pointAssemblyTransactionService;
    private readonly ILogger<PointSendTransactionWorker> _logger;
    private readonly IOptionsMonitor<WorkerOptions> _workerOptionsMonitor;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly string _lockKey = "IPointSendTransactionWorker";

    public PointSendTransactionWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IPointAssemblyTransactionService pointAssemblyTransactionService,
        ILogger<PointSendTransactionWorker> logger, IOptionsMonitor<WorkerOptions> workerOptionsMonitor,
        IAbpDistributedLock distributedLock) : base(
        timer, serviceScopeFactory)
    {
        _pointAssemblyTransactionService = pointAssemblyTransactionService;
        _logger = logger;
        _workerOptionsMonitor = workerOptionsMonitor;
        _distributedLock = distributedLock;
        timer.Period = _workerOptionsMonitor.CurrentValue.GetWorkerPeriodMinutes(_lockKey) * 60 * 1000;
    }
    
    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        await using var handle = await _distributedLock.TryAcquireAsync(_lockKey);
        var openSwitch = _workerOptionsMonitor.CurrentValue.GetWorkerSwitch(_lockKey);
        _logger.LogInformation("Executing point send transaction job start openSwitch {openSwitch}", openSwitch);
        if (!openSwitch)
        {
            _logger.LogWarning("PointSendTransactionWorker has not open...");
            return;
        }
        var chainIds = _workerOptionsMonitor.CurrentValue.ChainIds;
        foreach (var chainId in chainIds)
        {
            await _pointAssemblyTransactionService.SendAsync(chainId);
        }

        _logger.LogInformation("Executing point send transaction job end");
    }
}