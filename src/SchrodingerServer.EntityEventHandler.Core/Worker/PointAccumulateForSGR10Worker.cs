using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Dtos.Uniswap;
using SchrodingerServer.EntityEventHandler.Core.Options;
using SchrodingerServer.Grains.Grain.Points;
using SchrodingerServer.Points.Provider;
using SchrodingerServer.Uniswap;
using SchrodingerServer.Uniswap.Provider;
using SchrodingerServer.Users.Eto;
using Volo.Abp;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Caching;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Threading;
using DistributedCacheEntryOptions = Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions;

namespace SchrodingerServer.EntityEventHandler.Core.Worker;


public class PointAccumulateForSGR10Worker :  AsyncPeriodicBackgroundWorkerBase
{
    private const int MinimumIndexGap = 24;
    private const int SnapShotCount = 2;

    private readonly ILogger<PointAccumulateForSGR10Worker> _logger;
    private readonly IOptionsMonitor<WorkerOptions> _workerOptionsMonitor;
    private readonly IOptionsMonitor<PointTradeOptions> _pointTradeOptions;
    
    private readonly IPointDispatchProvider _pointDispatchProvider;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly IDistributedCache<List<int>> _distributedCache;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IClusterClient _clusterClient;
    private readonly IUniswapLiquidityService _uniswapLiquidityService;
    private readonly IUniswapLiquidityProvider _uniswapLiquidityProvider;
    private readonly string _lockKey = "PointAccumulateForSGR10Worker";

    public PointAccumulateForSGR10Worker(AbpAsyncTimer timer,IServiceScopeFactory serviceScopeFactory,ILogger<PointAccumulateForSGR10Worker> logger,
        IOptionsMonitor<WorkerOptions> workerOptionsMonitor,
        IClusterClient clusterClient,
        IDistributedCache<List<int>> distributedCache,
        IAbpDistributedLock distributedLock,
        IOptionsMonitor<PointTradeOptions> pointTradeOptions,
        IDistributedEventBus distributedEventBus,
        IPointDispatchProvider pointDispatchProvider,
        IUniswapLiquidityProvider uniswapLiquidityProvider,
        IUniswapLiquidityService uniswapLiquidityService): base(timer,
        serviceScopeFactory)
    {
        _logger = logger;
        _workerOptionsMonitor = workerOptionsMonitor;
        _clusterClient = clusterClient;
        _pointTradeOptions = pointTradeOptions;
        _distributedLock = distributedLock;
        _distributedCache = distributedCache;
        _distributedEventBus = distributedEventBus;
        _pointDispatchProvider = pointDispatchProvider;
        _uniswapLiquidityService = uniswapLiquidityService;
        _uniswapLiquidityProvider = uniswapLiquidityProvider;
        timer.Period = _workerOptionsMonitor.CurrentValue.GetWorkerPeriodMinutes(_lockKey) * 60 * 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        await using var handle = await _distributedLock.TryAcquireAsync(_lockKey);
        var openSwitch = _workerOptionsMonitor.CurrentValue.GetWorkerSwitch(_lockKey);
        var poolId = _pointTradeOptions.CurrentValue.UniswapPoolId;
        _logger.LogInformation("PointAccumulateForSGR10Worker start openSwitch {openSwitch}, pool:{pool}", openSwitch, poolId);
        if (!openSwitch)
        {
            _logger.LogWarning("PointAccumulateForSGR10Worker has not open...");
            return;
        }
        
        var pointName = _workerOptionsMonitor.CurrentValue.GetWorkerPointName(_lockKey);
        var bizDateList = _workerOptionsMonitor.CurrentValue.GetWorkerBizDateList(_lockKey);
        if (!bizDateList.IsNullOrEmpty())
        {
            foreach (var bizDate in bizDateList)
            {
                await SGR10SnapshotForOnceAsync(bizDate, poolId, pointName);
            }
        }
        else
        {
            var bizDate = DateTime.UtcNow.ToString(TimeHelper.Pattern);
            await SGR10SnapshotWorkAsync(bizDate, poolId, pointName);
        }
    }
    
    private async Task SGR10SnapshotForOnceAsync(string bizDate, string poolId, string pointName)
    {
        _logger.LogInformation("PointAccumulateForSGR10Worker SGR10SnapshotForOnceAsync date:{date} begin...", 
            bizDate);
        var isExecuted = await _pointDispatchProvider.GetDispatchAsync(PointDispatchConstants.SYNC_SGR10_PREFIX , bizDate, pointName);
        if (isExecuted)
        {
            _logger.LogInformation("PointAccumulateForSGR10Worker has been executed for bizDate: {0} pointName:{1}", bizDate, pointName);
            return;
        }
        
        await _uniswapLiquidityService.CreateSnapshotForOnceAsync(bizDate, poolId);
        
        await Task.Delay(3000);
        var allSnapshots = await _uniswapLiquidityProvider.GetAllSnapshotAsync(bizDate);
        _logger.LogInformation("PointAccumulateForSGR10Worker  snapshot counts: {cnt}", allSnapshots.Count);
        
        
        var validIds = await _uniswapLiquidityService.GetValidPositionIdsAsync(poolId, bizDate);
        var validSnapshots = allSnapshots.Where(x => validIds.Contains(x.PositionId)).ToList();
        _logger.LogInformation("PointAccumulateForSGR10Worker valid snapshot counts: {cnt}", validSnapshots.Count);
        
        var snapshotByAddress = validSnapshots.GroupBy(snapshot => snapshot.PositionOwner).Select(group =>
        {
            return new PositionPointDto
            {
                PositionOwner = group.Key,
                PointAmount = group.Sum(item => decimal.Parse(item.PointAmount)),
                BizDate = bizDate
            };
        }).ToList();
        _logger.LogInformation("PointAccumulateForSGR10Worker  snapshot by address counts: {cnt}", snapshotByAddress.Count);
        
        var now = DateTime.UtcNow;
        var chainId = _workerOptionsMonitor.CurrentValue.ChainIds.FirstOrDefault();
        foreach (var snapshot in snapshotByAddress)
        {
                
            var id = IdGenerateHelper.GetId(bizDate, pointName, snapshot.PositionOwner);
                
            var input = new PointDailyRecordGrainDto()
            {
                Id = id,
                ChainId = chainId,
                PointName = pointName,
                BizDate = bizDate,
                Address = snapshot.PositionOwner,
                HolderBalanceId = IdGenerateHelper.GetHolderBalanceId(chainId, "", snapshot.PositionOwner),
                PointAmount = snapshot.PointAmount
            };
      
            var pointDailyRecordGrain = _clusterClient.GetGrain<IPointDailyRecordGrain>(input.Id);
            var result = await pointDailyRecordGrain.UpdateAsync(input);
            _logger.LogDebug("PointAccumulateForSGR10Worker write grain result: {result}, record: {record}", 
                    JsonConvert.SerializeObject(result), JsonConvert.SerializeObject(input));

            if (!result.Success)
            {
                _logger.LogError(
                        "Handle Point Daily Record fail, id: {id}.", input.Id);
                    throw new UserFriendlyException($"Update Grain fail, id: {input.Id}.");
            }
            
            var pointDailyRecordEto = new PointDailyRecordEto
            {
                Id = id,
                Address = snapshot.PositionOwner,
                PointName = pointName,
                BizDate = bizDate,
                CreateTime = now,
                UpdateTime = now,
                ChainId = chainId,
                PointAmount = snapshot.PointAmount
            };
                
            await  _distributedEventBus.PublishAsync(pointDailyRecordEto);
        }
        
        
        await _pointDispatchProvider.SetDispatchAsync(PointDispatchConstants.SYNC_SGR10_PREFIX, bizDate,
            pointName, true);
        _logger.LogInformation("PointAccumulateForSGR10Worker SGR10SnapshotForOnceAsync date:{date} end...", 
            bizDate);
    }
    
    private async Task SGR10SnapshotWorkAsync(string bizDate, string poolId, string pointName)
    {
        DateTime now = DateTime.UtcNow;;
        int curIndex = now.Hour * 6 + now.Minute / 10;
        var indexList = await _distributedCache.GetAsync(PointDispatchConstants.SGR10_SNAPSHOT_INDEX_CACHE_KEY_PREFIX + bizDate);
        if (indexList == null)
        {
            indexList = await SetSnapshotIndexCacheAsync(bizDate, curIndex);
        }
        
        _logger.LogInformation("PointAccumulateForSGR10Worker Index Judgement, {index1}, {index2}, {curIndex}", 
            indexList[0], indexList[1], curIndex);
        
        var fixedIndexList =  _pointTradeOptions.CurrentValue.IndexList;
        if (!fixedIndexList.IsNullOrEmpty())
        {
            indexList = fixedIndexList.ToList();
            _logger.LogInformation("PointAccumulateForSGR10Worker change snap index list to {index1}", indexList);
        }
        
        if (!indexList.Contains(curIndex))
        {
            return;
        }
        
        await _uniswapLiquidityService.CreateSnapshotAsync(bizDate, poolId);
        _logger.LogInformation("PointAccumulateForSGR10Worker CreateSnapshotAsync Finished");
        
        if (indexList.IndexOf(curIndex) != SnapShotCount - 1)
        {
            return;
        }
        
        _logger.LogInformation("PointAccumulateForSGR10Worker cal points");
        await Task.Delay(3000);
        var allSnapshots = await _uniswapLiquidityProvider.GetAllSnapshotAsync(bizDate);
        _logger.LogInformation("PointAccumulateForSGR10Worker snapshot counts: {cnt}", allSnapshots.Count);
        
        var validIds = await _uniswapLiquidityService.GetValidPositionIdsAsync(poolId, bizDate);
        var validSnapshots = allSnapshots.Where(x => validIds.Contains(x.PositionId)).ToList();
        _logger.LogInformation("PointAccumulateForSGR10Worker valid snapshot counts: {cnt}", validSnapshots.Count);
        
        var snapshotByAddress = validSnapshots.GroupBy(snapshot => snapshot.PositionOwner).Select(group =>
        {
            return new PositionPointDto
            {
                PositionOwner = group.Key,
                PointAmount = group.Sum(item => decimal.Parse(item.PointAmount))/SnapShotCount,
                BizDate = bizDate
            };
        }).ToList();
        _logger.LogInformation("PointAccumulateForSGR10Worker  snapshot by address counts: {cnt}", snapshotByAddress.Count);
        
        var chainId = _workerOptionsMonitor.CurrentValue.ChainIds.FirstOrDefault();
        foreach (var snapshot in snapshotByAddress)
        {
                
            var id = IdGenerateHelper.GetId(bizDate, pointName, snapshot.PositionOwner);
                
            var input = new PointDailyRecordGrainDto()
            {
                Id = id,
                ChainId = chainId,
                PointName = pointName,
                BizDate = bizDate,
                Address = snapshot.PositionOwner,
                HolderBalanceId = IdGenerateHelper.GetHolderBalanceId(chainId, "", snapshot.PositionOwner),
                PointAmount = snapshot.PointAmount
            };
      
            var pointDailyRecordGrain = _clusterClient.GetGrain<IPointDailyRecordGrain>(input.Id);
            var result = await pointDailyRecordGrain.UpdateAsync(input);
            _logger.LogDebug("PointAccumulateForSGR10Worker write grain result: {result}, record: {record}", 
                    JsonConvert.SerializeObject(result), JsonConvert.SerializeObject(input));

            if (!result.Success)
            {
                _logger.LogError(
                        "Handle Point Daily Record fail, id: {id}.", input.Id);
                    throw new UserFriendlyException($"Update Grain fail, id: {input.Id}.");
            }
            
            var pointDailyRecordEto = new PointDailyRecordEto
            {
                Id = id,
                Address = snapshot.PositionOwner,
                PointName = pointName,
                BizDate = bizDate,
                CreateTime = now,
                UpdateTime = now,
                ChainId = chainId,
                PointAmount = snapshot.PointAmount
            };
                
            await  _distributedEventBus.PublishAsync(pointDailyRecordEto);
        }

        _logger.LogInformation("PointAccumulateForSGR10Worker end...");
    }
    
    private async Task<List<int>> SetSnapshotIndexCacheAsync(string bizDate, int startIndex)
    {
        _logger.LogInformation("PointAccumulateForSGR10Worker startIndex: {index1}", startIndex);
        AssertHelper.IsTrue(120 - startIndex > MinimumIndexGap, "PointAccumulateForSGR10Worker minimum gap cannot be satisfied");

        int randomNumber1;
        int randomNumber2;

        if (startIndex >= 70)
        {
            randomNumber1 = startIndex;
            randomNumber2 = randomNumber1 + MinimumIndexGap;
        }
        else
        {
            Random random = new Random();
            randomNumber1 =  random.Next(startIndex, 70);
            randomNumber2 = random.Next(randomNumber1 + MinimumIndexGap, 120);
        }
        
        var data = new List<int>() { randomNumber1, randomNumber2};
        
        await _distributedCache.SetAsync(PointDispatchConstants.SGR10_SNAPSHOT_INDEX_CACHE_KEY_PREFIX + bizDate, data, new DistributedCacheEntryOptions()
        {
            SlidingExpiration = TimeSpan.FromDays(7)
        });
        
        _logger.LogInformation("PointAccumulateForSGR10Worker Generate Snapshot Index, {index1}, {index2}", randomNumber1, randomNumber2);
        
        return  data;
    }
}