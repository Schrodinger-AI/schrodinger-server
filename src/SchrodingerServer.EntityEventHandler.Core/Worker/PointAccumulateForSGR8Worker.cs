using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using SchrodingerServer.Awaken.Provider;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
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

public class PointAccumulateForSGR8Worker :  AsyncPeriodicBackgroundWorkerBase
{
    private readonly ILogger<PointAccumulateForSGR8Worker> _logger;
    private readonly IOptionsMonitor<WorkerOptions> _workerOptionsMonitor;
    
    private readonly IPointDispatchProvider _pointDispatchProvider;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IClusterClient _clusterClient;
    private readonly IAwakenLiquidityProvider _awakenLiquidityProvider;
    private readonly string _lockKey = "PointAccumulateForSGR8Worker";
    private const string pointName = "XPSGR-8";

    public PointAccumulateForSGR8Worker(AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<PointAccumulateForSGR8Worker> logger,
        IOptionsMonitor<WorkerOptions> workerOptionsMonitor,
        IAbpDistributedLock distributedLock,
        IDistributedEventBus distributedEventBus,
        IClusterClient clusterClient,
        IAwakenLiquidityProvider awakenLiquidityProvider,
        IPointDispatchProvider pointDispatchProvider): base(timer,
        serviceScopeFactory)
    {
        _logger = logger;
        _workerOptionsMonitor = workerOptionsMonitor;
        _distributedLock = distributedLock;
        _distributedEventBus = distributedEventBus;
        _pointDispatchProvider = pointDispatchProvider;
        _awakenLiquidityProvider = awakenLiquidityProvider;
        _clusterClient = clusterClient;
        timer.Period = _workerOptionsMonitor.CurrentValue.GetWorkerPeriodMinutes(_lockKey) * 60 * 1000;
    }
    
    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        await using var handle = await _distributedLock.TryAcquireAsync(_lockKey);
        var openSwitch = _workerOptionsMonitor.CurrentValue.GetWorkerSwitch(_lockKey);
        _logger.LogInformation("PointAccumulateForSGR8Worker start openSwitch {openSwitch}", openSwitch);
        if (!openSwitch)
        {
            _logger.LogWarning("PointAccumulateForSGR8Worker has not open...");
            return;
        }
        
        var bizDate = _workerOptionsMonitor.CurrentValue.GetWorkerBizDate(_lockKey);
        bizDate = DateTime.UtcNow.AddDays(-1).ToString(TimeHelper.Pattern);
        var beginTime = DateTime.UtcNow.AddDays(-1).Date;
        var endTime = DateTime.UtcNow.Date;
        await CalculatePointAsync(TimeHelper.ToUtcMilliSeconds(beginTime), TimeHelper.ToUtcMilliSeconds(endTime),
            bizDate);
        
        _logger.LogInformation("PointAccumulateForSGR8Worker end...");
    }
    
    private async Task CalculatePointAsync(long beginTime, long endTime, string bizDate)
     {
         _logger.LogInformation("PointAccumulateForSGR8Worker CalculatePointAsync date:{date}, from:{from}, to:{to} begin...", 
             bizDate, beginTime, endTime);
         var isExecuted = await _pointDispatchProvider.GetDispatchAsync(PointDispatchConstants.SYNC_SGR8_PREFIX , bizDate, pointName);
         if (isExecuted)
         {
             _logger.LogInformation("PointAccumulateForSGR8Worker has been executed for bizDate: {0} pointName:{1}", bizDate, pointName);
             return;
         }
        
         var chainId  = _workerOptionsMonitor.CurrentValue.ChainIds.FirstOrDefault();
         var recordList = await GetTradeRecordsAsync(beginTime, endTime);
         _logger.LogInformation("PointAccumulateForSGR8Worker GetAwakenTradeRecordsAsync, record count: {len}", recordList.Count);

         var validRecord = recordList.Where(i => i.Side == 0 && i.TradePair.Token0.Symbol == "SGR-1").ToList();
         _logger.LogInformation("PointAccumulateForSGR8Worker GetAwakenTradeRecordsAsync, valid record count: {len}", validRecord.Count);
         
         var now = DateTime.UtcNow;
         var validRecordByAddress = validRecord.GroupBy(record => record.Address).Select(group =>
         {
             var address = group.Key;
             var id = IdGenerateHelper.GetId(bizDate, pointName, address);
             return new PointDailyRecordGrainDto
             {
                 Id = id,
                 ChainId = chainId,
                 PointName = pointName,
                 BizDate = bizDate,
                 Address = address,
                 HolderBalanceId = IdGenerateHelper.GetHolderBalanceId(chainId, "", address),
                 PointAmount = group.Sum(item => item.TotalPriceInUsd) * 99 *
                               (decimal)Math.Pow(10, UniswapConstants.SGRDecimal),
                 CreateTime = now,
                 UpdateTime = now
             };
         }).ToList();
         _logger.LogInformation("PointAccumulateForSGR8Worker GetAwakenTradeRecordsAsync, valid record by address count: {len}", validRecordByAddress.Count);
         
         foreach (var record in validRecordByAddress)
         {
             var pointDailyRecordGrain = _clusterClient.GetGrain<IPointDailyRecordGrain>(record.Id);
             var result = await pointDailyRecordGrain.UpdateAsync(record);
             _logger.LogDebug("PointAccumulateForSGR8Worker write grain result: {result}", 
                 JsonConvert.SerializeObject(result));

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
         
         await _pointDispatchProvider.SetDispatchAsync(PointDispatchConstants.SYNC_SGR8_PREFIX, bizDate,
             pointName, true);
         await Task.Delay(5000);
         await _pointDispatchProvider.SetDispatchAsync(PointDispatchConstants.CAL_FINISH_PREFIX, bizDate, pointName, true);
         _logger.LogInformation("PointAccumulateForSGR8Worker CalculatePointAsync date:{date} end...", 
             bizDate);
     }
    
    private async Task<List<AwakenTradeRecord>> GetTradeRecordsAsync(long beginTime, long endTime)
    {
        var skipCount = 0;
        var maxResultCount = 100;
        var recordList = new List<AwakenTradeRecord>();
        while (true)
        {
            var result =
                await _awakenLiquidityProvider.GetAwakenTradeRecordsAsync(beginTime, endTime, skipCount,
                    maxResultCount);
            recordList.AddRange(result.Items);
            if (result.TotalCount == 0 || result.Items.Count < maxResultCount)
            {
                break;
            }

            skipCount += maxResultCount;
        }

        return recordList;
    }
}