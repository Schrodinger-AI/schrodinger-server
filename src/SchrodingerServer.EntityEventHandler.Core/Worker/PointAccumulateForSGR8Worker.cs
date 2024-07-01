// using System.Collections.Generic;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Logging;
// using Microsoft.Extensions.Options;
// using Orleans;
// using SchrodingerServer.Common.Options;
// using SchrodingerServer.EntityEventHandler.Core.Options;
// using SchrodingerServer.Points.Provider;
// using SchrodingerServer.Uniswap;
// using SchrodingerServer.Uniswap.Provider;
// using Volo.Abp.BackgroundWorkers;
// using Volo.Abp.Caching;
// using Volo.Abp.DistributedLocking;
// using Volo.Abp.EventBus.Distributed;
// using Volo.Abp.Threading;
//
// namespace SchrodingerServer.EntityEventHandler.Core.Worker;
//
// public class PointAccumulateForSGR8Worker :  AsyncPeriodicBackgroundWorkerBase
// {
//     private readonly ILogger<PointAccumulateForSGR8Worker> _logger;
//     private readonly IOptionsMonitor<WorkerOptions> _workerOptionsMonitor;
//     private readonly IOptionsMonitor<PointTradeOptions> _pointTradeOptions;
//     
//     private readonly IPointDispatchProvider _pointDispatchProvider;
//     private readonly IAbpDistributedLock _distributedLock;
//     private readonly IDistributedCache<List<int>> _distributedCache;
//     private readonly IDistributedEventBus _distributedEventBus;
//     private readonly string _lockKey = "PointAccumulateForSGR8Worker";
//
//     public PointAccumulateForSGR8Worker(AbpAsyncTimer timer,
//         IServiceScopeFactory serviceScopeFactory,
//         ILogger<PointAccumulateForSGR8Worker> logger,
//         IOptionsMonitor<WorkerOptions> workerOptionsMonitor,
//         IDistributedCache<List<int>> distributedCache,
//         IAbpDistributedLock distributedLock,
//         IOptionsMonitor<PointTradeOptions> pointTradeOptions,
//         IDistributedEventBus distributedEventBus,
//         IPointDispatchProvider pointDispatchProvider): base(timer,
//         serviceScopeFactory)
//     {
//         _logger = logger;
//         _workerOptionsMonitor = workerOptionsMonitor;
//         _pointTradeOptions = pointTradeOptions;
//         _distributedLock = distributedLock;
//         _distributedCache = distributedCache;
//         _distributedEventBus = distributedEventBus;
//         _pointDispatchProvider = pointDispatchProvider;
//         timer.Period = _workerOptionsMonitor.CurrentValue.GetWorkerPeriodMinutes(_lockKey) * 60 * 1000;
//     }
//     
//     
//     
// }