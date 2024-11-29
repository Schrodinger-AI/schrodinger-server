using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchrodingerServer.Common;
using SchrodingerServer.EntityEventHandler.Core.Options;
using SchrodingerServer.Points;
using SchrodingerServer.Points.Provider;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Threading;

namespace SchrodingerServer.EntityEventHandler.Core.Worker;

public class PointAssemblyTransactionWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IPointAssemblyTransactionService _pointAssemblyTransactionService;
    private readonly ILogger<PointAssemblyTransactionWorker> _logger;
    private readonly IOptionsMonitor<WorkerOptions> _workerOptionsMonitor;
    private readonly IPointDispatchProvider _pointDispatchProvider;
    private readonly IAbpDistributedLock _distributedLock;

    private readonly string _lockKey = "IPointAssemblyTransactionWorker";

    public PointAssemblyTransactionWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IPointAssemblyTransactionService pointAssemblyTransactionService,
        ILogger<PointAssemblyTransactionWorker> logger, IOptionsMonitor<WorkerOptions> workerOptionsMonitor,
        IAbpDistributedLock distributedLock,
        IPointDispatchProvider pointDispatchProvider) : base(timer, serviceScopeFactory)
    {
        _pointAssemblyTransactionService = pointAssemblyTransactionService;
        _logger = logger;
        _workerOptionsMonitor = workerOptionsMonitor;
        _pointDispatchProvider = pointDispatchProvider;
        _distributedLock = distributedLock;
        timer.Period = _workerOptionsMonitor.CurrentValue.GetWorkerPeriodMinutes(_lockKey) * 60 * 1000;
    }
    
    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        await using var handle =
            await _distributedLock.TryAcquireAsync(_lockKey);
         var openSwitch = _workerOptionsMonitor.CurrentValue.GetWorkerSwitch(_lockKey);
        _logger.LogInformation("PointAssemblyTransactionWorker start openSwitch {openSwitch}", openSwitch);
        if (!openSwitch)
        {
            _logger.LogWarning("PointAssemblyTransactionWorker has not open...");
            return;
        }
        var txPointNames = _workerOptionsMonitor.CurrentValue.TxPointNames;
        foreach (var pointName in txPointNames)
        {
            var bizDateList = _workerOptionsMonitor.CurrentValue.GetWorkerBizDateList(_lockKey);
            //For batch execution
            if (!bizDateList.IsNullOrEmpty())
            {
                foreach (var bizDate in bizDateList)
                {
                    await DoPointAssemblyAsync(bizDate, pointName);
                }
            }
            else
            {
                var bizDate = _workerOptionsMonitor.CurrentValue.GetWorkerBizDate(_lockKey);
                if (bizDate.IsNullOrEmpty())
                {
                    bizDate = DateTime.UtcNow.AddDays(-1).ToString(TimeHelper.Pattern);
                }

                await DoPointAssemblyAsync(bizDate, pointName);
            }
        }
        _logger.LogInformation("PointAssemblyTransactionWorker end...");
    }

    private async Task DoPointAssemblyAsync(string bizDate, string pointName)
    {
        _logger.LogInformation("PointAssemblyTransactionWorker execute for bizDate: {0} pointName:{1}", bizDate, pointName);

        var isExecuted =  await _pointDispatchProvider.GetDispatchAsync(PointDispatchConstants.POINT_ASSEMBLY_TRANSACTION_PREFIX, bizDate, pointName);
        if (isExecuted)
        {
            _logger.LogInformation("PointAssemblyTransactionWorker has been executed for bizDate: {0} pointName: {1}", bizDate, pointName);
            return;
        }
        var readyForExecute =  await _pointDispatchProvider.GetDispatchAsync(PointDispatchConstants.CAL_FINISH_PREFIX, bizDate, pointName);
        bool alwaysCheck = IsAlwaysCheck(pointName);
        if (!readyForExecute && !alwaysCheck)
        {
            _logger.LogInformation("SyncHolderBalanceWorker has not ready for executed for bizDate: {0} pointName:{1}", bizDate, pointName);
            return;
        }
        var chainIds = _workerOptionsMonitor.CurrentValue.ChainIds;
        foreach (var chainId in chainIds)
        {
            await _pointAssemblyTransactionService.AssembleAsync(chainId, bizDate, pointName);
        }
        _logger.LogInformation("Executing point assembly transaction job end");
        await _pointDispatchProvider.SetDispatchAsync(PointDispatchConstants.POINT_ASSEMBLY_TRANSACTION_PREFIX, bizDate, pointName, true);
    }

    
    private bool IsAlwaysCheck(string pointName)
    {
        if (pointName == "XPSGR-7" || pointName == "XPSGR-8")
        {
            return false;
        }

        return true;
    }

}