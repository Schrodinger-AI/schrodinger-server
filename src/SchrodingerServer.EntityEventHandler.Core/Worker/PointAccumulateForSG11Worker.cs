using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Newtonsoft.Json;
using NUglify.Helpers;
using Orleans;
using SchrodingerServer.Awaken.Provider;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.EntityEventHandler.Core.Options;
using SchrodingerServer.Grains.Grain.Points;
using SchrodingerServer.Points;
using SchrodingerServer.Points.Provider;
using SchrodingerServer.Users.Eto;
using SchrodingerServer.Users.Index;
using Volo.Abp;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Caching;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Threading;
using DistributedCacheEntryOptions = Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions;

namespace SchrodingerServer.EntityEventHandler.Core.Worker;


public class PointAccumulateForSGR11Worker :  AsyncPeriodicBackgroundWorkerBase
{
    private const int MaxResultCount = 2000;
    private const int MinimumIndexGap = 24;
    private const int SnapShotCount = 2;
    private const string USDT = "USDT";
    private const string SGR = "SGR-1";
    private const string ELF = "ELF";

    private readonly ILogger<PointAccumulateForSGR11Worker> _logger;
    private readonly IOptionsMonitor<WorkerOptions> _workerOptionsMonitor;
    private readonly IOptionsMonitor<PointTradeOptions> _pointTradeOptions;
    private readonly INESTRepository<AwakenLiquiditySnapshotIndex, string> _pointSnapshotIndexRepository;
    
    private readonly IAwakenLiquidityProvider _awakenLiquidityProvider;
    private readonly IPointDispatchProvider _pointDispatchProvider;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly IDistributedCache<List<int>> _distributedCache;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IClusterClient _clusterClient;
    private readonly string _lockKey = "PointAccumulateForSGR11Worker";

    public PointAccumulateForSGR11Worker(AbpAsyncTimer timer,IServiceScopeFactory serviceScopeFactory,ILogger<PointAccumulateForSGR11Worker> logger,
        IOptionsMonitor<WorkerOptions> workerOptionsMonitor,
        IClusterClient clusterClient,
        IDistributedCache<List<int>> distributedCache,
        IAwakenLiquidityProvider awakenLiquidityProvider,
        IAbpDistributedLock distributedLock,
        IOptionsMonitor<PointTradeOptions> pointTradeOptions,
        IDistributedEventBus distributedEventBus,
        INESTRepository<AwakenLiquiditySnapshotIndex, string> pointSnapshotIndexRepository): base(timer,
        serviceScopeFactory)
    {
        _logger = logger;
        _workerOptionsMonitor = workerOptionsMonitor;
        _clusterClient = clusterClient;
        _pointTradeOptions = pointTradeOptions;
        _distributedLock = distributedLock;
        _awakenLiquidityProvider = awakenLiquidityProvider;
        _distributedCache = distributedCache;
        _pointSnapshotIndexRepository = pointSnapshotIndexRepository;
        _distributedEventBus = distributedEventBus;
        timer.Period = _workerOptionsMonitor.CurrentValue.GetWorkerPeriodMinutes(_lockKey) * 60 * 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        await using var handle = await _distributedLock.TryAcquireAsync(_lockKey);
        var openSwitch = _workerOptionsMonitor.CurrentValue.GetWorkerSwitch(_lockKey);
        var poolId = _pointTradeOptions.CurrentValue.AwakenPoolId;
        _logger.LogInformation("PointAccumulateForSGR11Worker start openSwitch {openSwitch}, pool:{pool}", openSwitch, poolId);
        if (!openSwitch)
        {
            _logger.LogWarning("PointAccumulateForSGR11Worker has not open...");
            return;
        }
        
        var pointName = _workerOptionsMonitor.CurrentValue.GetWorkerPointName(_lockKey);
        var bizDateList = _workerOptionsMonitor.CurrentValue.GetWorkerBizDateList(_lockKey);
        if (!bizDateList.IsNullOrEmpty())
        {
            foreach (var bizDate in bizDateList)
            {
                await SGR11SnapshotForOnceAsync(bizDate, pointName);
            }
        }
        else
        {
            var bizDate = DateTime.UtcNow.ToString(TimeHelper.Pattern);
            await SGR11SnapshotWorkAsync(bizDate, pointName);
        }
    }
    
    
    private async Task SGR11SnapshotWorkAsync(string bizDate, string pointName)
    {
        DateTime now = DateTime.Now;
        int curIndex = now.Hour * 6 + now.Minute / 10;
        var indexList = await _distributedCache.GetAsync(PointDispatchConstants.SGR11_SNAPSHOT_INDEX_CACHE_KEY_PREFIX + bizDate);
        if (indexList == null)
        {
            indexList = await SetSnapshotIndexCacheAsync(bizDate, curIndex);
        }
        
        _logger.LogInformation("PointAccumulateForSGR11Worker Index Judgement, {index1}, {index2}, {curIndex}", 
            indexList[0], indexList[1], curIndex);
        
        var manaliIndexList =  _pointTradeOptions.CurrentValue.IndexList;
        if (!manaliIndexList.IsNullOrEmpty())
        {
            indexList = manaliIndexList.ToList();
            _logger.LogInformation("PointAccumulateForSGR9Worker change snap index list to {index1}", indexList);
        }
        
        if (!indexList.Contains(curIndex))
        {
            return;
        }
        
        var chainIds = _workerOptionsMonitor.CurrentValue.ChainIds;
        foreach (var chainId in chainIds)
        {
            await GenerateSnapshotAsync(chainId, bizDate, pointName, indexList.IndexOf(curIndex), true);
        }

        _logger.LogInformation("PointAccumulateForSGR11Worker end...");
    }
    
    private async Task SGR11SnapshotForOnceAsync(string bizDate, string pointName)
    {
        _logger.LogInformation("PointAccumulateForSGR11Worker SGR11SnapshotForOnceAsync date:{date} begin...", 
            bizDate);
        var isExecuted = await _pointDispatchProvider.GetDispatchAsync(PointDispatchConstants.SYNC_SGR11_PREFIX , bizDate, pointName);
        if (isExecuted)
        {
            _logger.LogInformation("PointAccumulateForSGR11Worker has been executed for bizDate: {0} pointName:{1}", bizDate, pointName);
            return;
        }
        
        var chainIds = _workerOptionsMonitor.CurrentValue.ChainIds;
        foreach (var chainId in chainIds)
        {
            await GenerateSnapshotAsync(chainId, bizDate, pointName, 1, false);
        }

        _logger.LogInformation("PointAccumulateForSGR11Worker SGR11SnapshotForOnceAsync date:{date} end...", 
            bizDate);
        await _pointDispatchProvider.SetDispatchAsync(PointDispatchConstants.SYNC_SGR11_PREFIX, bizDate,
            pointName, true);
    }


    private async Task GenerateSnapshotAsync(string chainId, string bizDate, string pointName, int snapshotIndex, bool isCurrent)
    {
        var now = DateTime.UtcNow;
        var ts = TimeHelper.GetTimeStampInMilliseconds();
        if (!isCurrent)
        {
            var backThen = DateTime.ParseExact(bizDate, "yyyyMMdd", null);
            ts = new DateTimeOffset(backThen).ToUnixTimeMilliseconds();
        }
        
        var request = new GetAwakenLiquidityRecordDto
        {
            SkipCount = 0,
            MaxResultCount = MaxResultCount,
            ChainId = chainId,
            Pair = _pointTradeOptions.CurrentValue.AwakenPoolId,
            TimestampMax = ts
        };

        var res = await _awakenLiquidityProvider.GetLiquidityRecordsAsync(request);
        _logger.LogInformation("PointAccumulateForSGR11Worker GetLiquidityRecordsAsync request: {result}, result: {record}", 
            JsonConvert.SerializeObject(request), JsonConvert.SerializeObject(res));
        
        
        var validRecord = res.Where(x => x.Type == "MINT" || x.Type == "BURN").ToList();
        validRecord.ForEach(x =>
        {
            x.Token0Amount = x.Type == "MINT" ? x.Token0Amount : -x.Token0Amount;
            x.Token1Amount = x.Type == "MINT" ? x.Token1Amount : -x.Token1Amount;

            if (x.Token0 == ELF)
            {
                (x.Token0Amount, x.Token1Amount) = (x.Token1Amount, x.Token0Amount);
            }
        });
        
        var snapshots = res.GroupBy(snapshot => snapshot.Address).Select(group => new AwakenLiquiditySnapshotIndex
            {
                Id = IdGenerateHelper.GetId(group.Key, now.ToString("yyyy-MM-dd HH:mm")),
                Address = group.Key,
                Token0Amount = group.Sum(item => item.Token0Amount),
                Token1Amount = group.Sum(item => item.Token1Amount),
                Token0Name = SGR,
                Token1Name = ELF,
                CreateTime = now,
                BizDate = bizDate
            }).ToList();
        _logger.LogInformation("PointAccumulateForSGR11Worker  liquidity address record counts: {cnt}", snapshots.Count);

        var validSnapshots = snapshots.Where(x => x.Token0Amount >= 0 && x.Token1Amount >= 0).ToList();
        
        _logger.LogInformation("PointAccumulateForSGR11Worker  valid record counts: {cnt}", validSnapshots.Count);
        await _pointSnapshotIndexRepository.BulkAddOrUpdateAsync(validSnapshots);
        
        
        if (snapshotIndex == SnapShotCount-1)
        {
            _logger.LogInformation("PointAccumulateForSGR11Worker cal points");
            await Task.Delay(3000);
            var allSnapshots = await GetAllIndex(bizDate, pointName);
            _logger.LogInformation("PointAccumulateForSGR11Worker  snapshot counts: {cnt}", allSnapshots.Count);

            var snapshotByAddress = allSnapshots.GroupBy(snapshot => snapshot.Address).Select(group => new AwakenLiquiditySnapshotIndex
                {
                    Address = group.Key,
                    Token0Amount = group.Sum(item => item.Token0Amount) / SnapShotCount,
                    Token1Amount = group.Sum(item => item.Token1Amount) / SnapShotCount,
                    PointName = pointName,
                    BizDate = bizDate
                });
            
            _logger.LogInformation("PointAccumulateForSGR11Worker  snapshot by address counts: {cnt}", snapshotByAddress.Count());

            var validAddress = await GetValidAddressAsync(chainId, bizDate);
            _logger.LogInformation("PointAccumulateForSGR11Worker  get valid address counts: {cnt}", validAddress.Count());
            
            var elfPrice = await GetELFPrice();
            var sgrPrice = await GetSGRPrice() * elfPrice;
            
            _logger.LogInformation("PointAccumulateForSGR11Worker  get prices in USD, ELF: {elf}, SGR: {sgr}", elfPrice, sgrPrice);

            
            foreach (var snapshot in snapshotByAddress)
            {
                if (!validAddress.Contains(snapshot.Address))
                {
                    _logger.LogInformation("PointAccumulateForSGR11Worker Holding Less Than 24hours, address: {address}", snapshot.Address);
                    continue;
                }
                
                var id = IdGenerateHelper.GetId(chainId, bizDate, pointName, snapshot.Address);
                var pointAmount = (snapshot.Token0Amount * sgrPrice + snapshot.Token1Amount * elfPrice) * 99 /
                                  100000000;
                var input = new PointDailyRecordGrainDto()
                {
                    ChainId = chainId,
                    PointName = pointName,
                    BizDate = bizDate,
                    Address = snapshot.Address,
                    HolderBalanceId = IdGenerateHelper.GetHolderBalanceId(chainId, "", snapshot.Address),
                    PointAmount = pointAmount
                };
                input.Id = id;
                var pointDailyRecordGrain = _clusterClient.GetGrain<IPointDailyRecordGrain>(input.Id);
                var result = await pointDailyRecordGrain.UpdateAsync(input);
                _logger.LogDebug("PointAccumulateForSGR11Worker write grain result: {result}, record: {record}", 
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
                    Address = snapshot.Address,
                    PointName = pointName,
                    BizDate = bizDate,
                    CreateTime = now,
                    UpdateTime = now,
                    ChainId = chainId,
                    PointAmount = pointAmount
                };
                
                
                await  _distributedEventBus.PublishAsync(pointDailyRecordEto);
            }
        }
    }
    
    private async  Task<List<string>> GetValidAddressAsync(string chainId, string bizDate)
    {
        DateTime currentUtc = DateTime.ParseExact(bizDate, "yyyyMMdd", null); 
        DateTime targetUtc = currentUtc.AddHours(-24).Date;

        DateTime targetUtcMidnight =
            new DateTime(targetUtc.Year, targetUtc.Month, targetUtc.Day, 0, 0, 0, DateTimeKind.Utc);
        
        long milliseconds = new DateTimeOffset(targetUtcMidnight).ToUnixTimeMilliseconds();
        
        _logger.LogInformation("PointAccumulateForSGR11Worker get time in targetUtc: {targetUtc} targetUtcMidnight: {targetUtcMidnight}", targetUtc, targetUtcMidnight);
        
        var request = new GetAwakenLiquidityRecordDto
        {
            SkipCount = 0,
            MaxResultCount = MaxResultCount,
            ChainId = chainId,
            Pair = _pointTradeOptions.CurrentValue.AwakenPoolId,
            TimestampMax = milliseconds
        };

        var res = await _awakenLiquidityProvider.GetLiquidityRecordsAsync(request);
        var validRecord = res.Where(x => x.Type == "MINT" || x.Type == "BURN").ToList();
        validRecord.ForEach(x =>
        {
            x.Token0Amount = x.Type == "MINT" ? x.Token0Amount : -x.Token0Amount;
            x.Token1Amount = x.Type == "MINT" ? x.Token1Amount : -x.Token1Amount;

            if (x.Token0 == ELF)
            {
                (x.Token0Amount, x.Token1Amount) = (x.Token1Amount, x.Token0Amount);
            }
        });
        
        var snapshots = res.GroupBy(snapshot => snapshot.Address).Select(group => new AwakenLiquiditySnapshotIndex
        {
            Address = group.Key,
            Token0Amount = group.Sum(item => item.Token0Amount),
            Token1Amount = group.Sum(item => item.Token1Amount),
        }).ToList();
        _logger.LogInformation("PointAccumulateForSGR11Worker get snapshot in ts: {ts} counts: {cnt}", milliseconds, snapshots.Count);

        var validSnapshots = snapshots.Where(x => x.Token0Amount >= 0 && x.Token1Amount >= 0).ToList();
        _logger.LogInformation("PointAccumulateForSGR11Worker get valid snapshot in ts: {ts} counts: {cnt}", milliseconds, snapshots.Count);
        var address = validSnapshots.Select(x => x.Address).ToList();
        return address;
    }
    
    private async Task<decimal> GetELFPrice()
    {
        var priceDto = await _awakenLiquidityProvider.GetPriceAsync(USDT, "tDVV", "0.0005");
        var price = priceDto.Items.FirstOrDefault().Price;
        AssertHelper.IsTrue(price != null && price > 0, "SGR price is null or zero");
        return price;
    }
    
    
    private async Task<decimal> GetSGRPrice()
    {
        var priceDto = await _awakenLiquidityProvider.GetPriceAsync(SGR, "tDVV", "0.03");
        var price = priceDto.Items.FirstOrDefault().Price;
        AssertHelper.IsTrue(price != null && price > 0, "SGR price is null or zero");
        return 1 / price;
    }
    
    private  async Task<List<AwakenLiquiditySnapshotIndex>> GetAllIndex(string bizDate, string pointName)
    {
        var res = new List<AwakenLiquiditySnapshotIndex>();
        List<AwakenLiquiditySnapshotIndex> list;
        var skipCount = 0;
        var mustQuery = new List<Func<QueryContainerDescriptor<AwakenLiquiditySnapshotIndex>, QueryContainer>>();
        
        mustQuery.Add(q => q.Term(i
            => i.Field(index => index.BizDate).Value(bizDate)));
        
        QueryContainer Filter(QueryContainerDescriptor<AwakenLiquiditySnapshotIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        do
        {
            list = (await _pointSnapshotIndexRepository.GetListAsync(filterFunc: Filter, skip: skipCount, limit: 10000)).Item2;
            var count = list.Count;
            res.AddRange(list);
            if (list.IsNullOrEmpty() || count < 10000)
            {
                break;
            }
            skipCount += count;
        } while (!list.IsNullOrEmpty());

        return res;
    }
    
    private async Task<List<int>> SetSnapshotIndexCacheAsync(string bizDate, int startIndex)
    {
        _logger.LogInformation("PointAccumulateForSGR11Worker startIndex: {index1}", startIndex);
        AssertHelper.IsTrue(120 - startIndex > MinimumIndexGap, "PointAccumulateForSGR11Worker minimum gap cannot be satisfied");

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
        
        await _distributedCache.SetAsync(PointDispatchConstants.SGR11_SNAPSHOT_INDEX_CACHE_KEY_PREFIX + bizDate, data, new DistributedCacheEntryOptions()
        {
            SlidingExpiration = TimeSpan.FromDays(7)
        });
        
        _logger.LogInformation("PointAccumulateForSGR11Worker Generate Snapshot Index, {index1}, {index2}", randomNumber1, randomNumber2);
        
        return  data;
    }
}