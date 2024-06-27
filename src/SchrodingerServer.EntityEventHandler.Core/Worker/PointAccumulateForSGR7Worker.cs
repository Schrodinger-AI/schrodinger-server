using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchrodingerServer.Cat.Provider;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Dtos.Cat;
using SchrodingerServer.EntityEventHandler.Core.Options;
using SchrodingerServer.Points.Provider;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Caching;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Threading;

namespace SchrodingerServer.EntityEventHandler.Core.Worker;

public class PointAccumulateForSGR7Worker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly ILogger<PointAccumulateForSGR7Worker> _logger;
     private readonly IOptionsMonitor<WorkerOptions> _workerOptionsMonitor;
     private readonly IOptionsMonitor<PointTradeOptions> _pointTradeOptions;
     
     private readonly IPointDispatchProvider _pointDispatchProvider;
     private readonly IAbpDistributedLock _distributedLock;
     private readonly IDistributedCache<List<int>> _distributedCache;
     private readonly IDistributedEventBus _distributedEventBus;
     private readonly ISchrodingerCatProvider _schrodingerCatProvider;
     private readonly string _lockKey = "PointAccumulateForSGR7Worker";

     public PointAccumulateForSGR7Worker(AbpAsyncTimer timer,
         IServiceScopeFactory serviceScopeFactory,
         ILogger<PointAccumulateForSGR7Worker> logger,
         IOptionsMonitor<WorkerOptions> workerOptionsMonitor,
         IDistributedCache<List<int>> distributedCache,
         IAbpDistributedLock distributedLock,
         IOptionsMonitor<PointTradeOptions> pointTradeOptions,
         IDistributedEventBus distributedEventBus,
         ISchrodingerCatProvider schrodingerCatProvider,
         IPointDispatchProvider pointDispatchProvider): base(timer,
         serviceScopeFactory)
     {
         _logger = logger;
         _workerOptionsMonitor = workerOptionsMonitor;
         _pointTradeOptions = pointTradeOptions;
         _distributedLock = distributedLock;
         _distributedCache = distributedCache;
         _distributedEventBus = distributedEventBus;
         _pointDispatchProvider = pointDispatchProvider;
         _schrodingerCatProvider = schrodingerCatProvider;
         timer.Period = _workerOptionsMonitor.CurrentValue.GetWorkerPeriodMinutes(_lockKey) * 60 * 1000;
     }
     
     protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
     {
         await using var handle = await _distributedLock.TryAcquireAsync(_lockKey);
         var openSwitch = _workerOptionsMonitor.CurrentValue.GetWorkerSwitch(_lockKey);
         _logger.LogInformation("PointAccumulateForSGR7Worker start openSwitch {openSwitch}", openSwitch);
         if (!openSwitch)
         {
             _logger.LogWarning("PointAccumulateForSGR7Worker has not open...");
             return;
         }
       
         var pointName = _workerOptionsMonitor.CurrentValue.GetWorkerPointName(_lockKey);
        
         var bizDate = _workerOptionsMonitor.CurrentValue.GetWorkerBizDate(_lockKey);
         if (bizDate.IsNullOrEmpty())
         {
             bizDate = DateTime.UtcNow.ToString(TimeHelper.Pattern);
         }

         await DoSyncHolderBalance(bizDate, pointName);    
        
         _logger.LogInformation("PointAccumulateForSGR7Worker end...");
     }
     
     private async Task CalculatePointAsync(long beginTime, long endTime)
     {
         var chainId  = _workerOptionsMonitor.CurrentValue.ChainIds.FirstOrDefault();
         var input = new GetSchrodingerSoldInput
         {
             TimestampMax = endTime,
             TimestampMin = beginTime,
             ChainId = chainId,
             FilterSymbol = chainId == "tDVV" ? "SGR" : "SGRTEST"
         };
         var soldList = await _schrodingerCatProvider.GetSchrodingerSoldListAsync(input);
      
         var soldByToAddress = soldList.GroupBy(x => x.To).Select(g =>
         {
             
         })
     }
     
     
}