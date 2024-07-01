using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using SchrodingerServer.Awaken.Provider;
using SchrodingerServer.Cat.Provider;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Dtos.Cat;
using SchrodingerServer.EntityEventHandler.Core.Options;
using SchrodingerServer.Grains.Grain.Points;
using SchrodingerServer.Points.Provider;
using SchrodingerServer.Uniswap;
using SchrodingerServer.Users.Eto;
using Volo.Abp;
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
     private readonly IClusterClient _clusterClient;
     private readonly IAwakenLiquidityProvider _awakenLiquidityProvider;
     private readonly string _lockKey = "PointAccumulateForSGR7Worker";
     private const string pointName = "XPSGR-7";

     public PointAccumulateForSGR7Worker(AbpAsyncTimer timer,
         IServiceScopeFactory serviceScopeFactory,
         ILogger<PointAccumulateForSGR7Worker> logger,
         IOptionsMonitor<WorkerOptions> workerOptionsMonitor,
         IDistributedCache<List<int>> distributedCache,
         IAbpDistributedLock distributedLock,
         IOptionsMonitor<PointTradeOptions> pointTradeOptions,
         IDistributedEventBus distributedEventBus,
         ISchrodingerCatProvider schrodingerCatProvider,
         IClusterClient clusterClient,
         IAwakenLiquidityProvider awakenLiquidityProvider,
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
         _clusterClient = clusterClient;
         _awakenLiquidityProvider = awakenLiquidityProvider;
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
        
         var bizDate = _workerOptionsMonitor.CurrentValue.GetWorkerBizDate(_lockKey);
         if (bizDate.IsNullOrEmpty())
         {
             bizDate = DateTime.UtcNow.AddDays(-1).ToString(TimeHelper.Pattern);
             var beginTime = DateTime.UtcNow.AddDays(-1).Date;
             var endTime = DateTime.UtcNow.Date;
             await CalculatePointAsync(TimeHelper.ToUtcMilliSeconds(beginTime), TimeHelper.ToUtcMilliSeconds(endTime),
                 bizDate);
         }
         else
         {
             // compensate history points
             bizDate = DateTime.UtcNow.AddDays(-1).ToString(TimeHelper.Pattern);
             var beginTime = new DateTime(2024, 3, 1);
             var endTime = DateTime.UtcNow.Date;
             await CalculatePointAsync(TimeHelper.ToUtcMilliSeconds(beginTime), TimeHelper.ToUtcMilliSeconds(endTime),
                 bizDate);
         }
        
         _logger.LogInformation("PointAccumulateForSGR7Worker end...");
     }
     
     private async Task CalculatePointAsync(long beginTime, long endTime, string bizDate)
     {
         _logger.LogInformation("PointAccumulateForSGR7Worker CalculatePointAsync date:{date}, from:{from}, to:{to} begin...", 
             bizDate, beginTime, endTime);
         var isExecuted = await _pointDispatchProvider.GetDispatchAsync(PointDispatchConstants.SYNC_SGR7_PREFIX , bizDate, pointName);
         if (isExecuted)
         {
             _logger.LogInformation("PointAccumulateForSGR7Worker has been executed for bizDate: {0} pointName:{1}", bizDate, pointName);
             return;
         }
         
         var chainId  = _workerOptionsMonitor.CurrentValue.ChainIds.FirstOrDefault();
         var input = new GetSchrodingerSoldInput
         {
             TimestampMax = endTime,
             TimestampMin = beginTime,
             ChainId = chainId,
             FilterSymbol = chainId == "tDVV" ? "SGR" : "SGRTEST"
         };
         var soldList = await _schrodingerCatProvider.GetSchrodingerSoldListAsync(input);
         
         
         var priceDto = await _awakenLiquidityProvider.GetPriceAsync("ELF", "USDT", "tDVV", "0.0005");
         var price = priceDto.Items.FirstOrDefault().Price;
         AssertHelper.IsTrue(price != null && price > 0, "ELF price is null or zero");

         var now = DateTime.UtcNow;
         var soldByToAddress = soldList.GroupBy(x => x.To).Select(g =>
         {
             var address = FullAddressHelper.ToShortAddress(g.Key);
             var id = IdGenerateHelper.GetId(bizDate, pointName, address);
             return new PointDailyRecordGrainDto
             {
                 Id = id,
                 ChainId = chainId,
                 PointName = pointName,
                 BizDate = bizDate,
                 Address = address,
                 HolderBalanceId = IdGenerateHelper.GetHolderBalanceId(chainId, "", address),
                 PointAmount = g.Sum(item => item.Amount * item.Price) * price * 99 * (decimal)Math.Pow(10, UniswapConstants.SGRDecimal),
                 CreateTime = now,
                 UpdateTime = now
             };
         }).ToList();
         
         foreach (var record in soldByToAddress)
         {
             var pointDailyRecordGrain = _clusterClient.GetGrain<IPointDailyRecordGrain>(record.Id);
             var result = await pointDailyRecordGrain.UpdateAsync(record);
             _logger.LogDebug("PointAccumulateForSGR7Worker write grain result: {result}, record: {record}", 
                 JsonConvert.SerializeObject(result), JsonConvert.SerializeObject(input));

             if (!result.Success)
             {
                 _logger.LogError(
                     "Handle Point Daily Record fail, id: {id}.", record.Id);
                 throw new UserFriendlyException($"Update Grain fail, id: {record.Id}.");
             }
            
             var pointDailyRecordEto = new PointDailyRecordEto
             {
                 Id = record.Id,
                 Address = record.Address,
                 PointName = pointName,
                 BizDate = bizDate,
                 CreateTime = now,
                 UpdateTime = now,
                 ChainId = chainId,
                 PointAmount = record.PointAmount
             };
                
             await  _distributedEventBus.PublishAsync(pointDailyRecordEto);
         }
         
         await _pointDispatchProvider.SetDispatchAsync(PointDispatchConstants.SYNC_SGR7_PREFIX, bizDate,
             pointName, true);
         _logger.LogInformation("PointAccumulateForSGR7Worker CalculatePointAsync date:{date} end...", 
             bizDate);
     }
     
     
}