using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using NUglify.Helpers;
using SchrodingerServer.Awaken.Provider;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.EntityEventHandler.Core.Options;
using SchrodingerServer.Points;
using SchrodingerServer.Points.Provider;
using SchrodingerServer.Users.Eto;
using SchrodingerServer.Users.Index;
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

    private readonly ILogger<SyncHolderBalanceWorker> _logger;
    private readonly IOptionsMonitor<WorkerOptions> _workerOptionsMonitor;
    private readonly IOptionsMonitor<PointTradeOptions> _pointTradeOptions;
    private readonly INESTRepository<AwakenLiquiditySnapshotIndex, string> _pointSnapshotIndexRepository;

    private readonly IObjectMapper _objectMapper;
    private readonly IAwakenLiquidityProvider _awakenLiquidityProvider;
    private readonly IPointDispatchProvider _pointDispatchProvider;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly IDistributedCache<List<int>> _distributedCache;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly string _lockKey = "PointAccumulateForSGR11Worker";

    public PointAccumulateForSGR11Worker(AbpAsyncTimer timer,IServiceScopeFactory serviceScopeFactory,ILogger<SyncHolderBalanceWorker> logger,
        IOptionsMonitor<WorkerOptions> workerOptionsMonitor,
        IObjectMapper objectMapper,
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
        //
        // var bizDate = _workerOptionsMonitor.CurrentValue.GetWorkerBizDate(_lockKey);
        // if (bizDate.IsNullOrEmpty())
        // {
        //     bizDate = DateTime.UtcNow.ToString(TimeHelper.Pattern);
        // }
        //
        // await DoSyncHolderBalance(bizDate, pointName);   
        
        var bizDate = DateTime.UtcNow.ToString(TimeHelper.Pattern);
        
        DateTime now = DateTime.Now;
        int curIndex = now.Hour * 6 + now.Minute / 10;
        var indexList = await _distributedCache.GetAsync(PointDispatchConstants.SGR11_SNAPSHOT_INDEX_CACHE_KEY_PREFIX + bizDate);
        if (indexList == null)
        {
            indexList = await SetSnapshotIndexCacheAsync(bizDate, curIndex);
        }
        
        _logger.LogInformation("PointAccumulateForSGR11Worker Index Judgement, {index1}, {index2}, {curIndex}", 
            indexList[0], indexList[1], curIndex);
        
        var manaliIndexList =  _pointTradeOptions.CurrentValue.indexList;
        if (!manaliIndexList.IsNullOrEmpty())
        {
            indexList = manaliIndexList.ToList();
            _logger.LogInformation("PointAccumulateForSGR9Worker change snap index list to {index1}", indexList);
        }
        
        if (!indexList.Contains(curIndex))
        {
            return;
        }
        
        await GenerateSnapshotAsync("tDVV", bizDate, pointName, indexList.IndexOf(curIndex));
        

        _logger.LogInformation("PointAccumulateForSGR11Worker end...");
    }


    private async Task GenerateSnapshotAsync(string chainId, string bizDate, string pointName, int snapshotIndex)
    {
        var request = new GetAwakenLiquidityRecordDto
        {
            SkipCount = 0,
            MaxResultCount = MaxResultCount,
            ChainId = chainId,
            Pair = _pointTradeOptions.CurrentValue.AwakenPoolId,
            TimestampMax = TimeHelper.GetTimeStampInMilliseconds()
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
        
        var now = DateTime.UtcNow;
        
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

            var elfPrice = await GetELFPrice(chainId);
            var sgrPrice = await GetSGRPrice(chainId) * elfPrice;
            
            foreach (var snapshot in snapshotByAddress)
            {
                var pointDailyRecordEto = new PointDailyRecordEto
                {
                    Id = IdGenerateHelper.GetId(chainId, bizDate, pointName, snapshot.Address),
                    Address = snapshot.Address,
                    PointName = pointName,
                    BizDate = bizDate,
                    CreateTime = now,
                    UpdateTime = now,
                    ChainId = chainId,
                    PointAmount = (snapshot.Token0Amount * sgrPrice + snapshot.Token1Amount * elfPrice) * 99 / 100000000 ,
                };
                
                
                await  _distributedEventBus.PublishAsync(pointDailyRecordEto);
            }
        }
    }
    
    private async Task<decimal> GetELFPrice(string chainId)
    {
        var priceDto = await _awakenLiquidityProvider.GetPriceAsync(USDT, "tDVV", "0.0005");
        var price = priceDto.Items.FirstOrDefault().Price;
        AssertHelper.IsTrue(price != null && price > 0, "SGR price is null or zero");
        return price;
    }
    
    
    private async Task<decimal> GetSGRPrice(string chainId)
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