using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using SchrodingerServer.Cat.Provider;
using SchrodingerServer.Cat.Provider.Dtos;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Dtos.Cat;
using SchrodingerServer.EntityEventHandler.Core.Options;
using SchrodingerServer.Points;
using SchrodingerServer.Points.Provider;
using SchrodingerServer.Symbol.Provider;
using SchrodingerServer.Users;
using SchrodingerServer.Users.Index;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Caching;
using Volo.Abp.DistributedLocking;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Threading;
using DistributedCacheEntryOptions = Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions;

namespace SchrodingerServer.EntityEventHandler.Core.Worker;


public class PointAccumulateForSGR9Worker :  AsyncPeriodicBackgroundWorkerBase
{
    private const int MaxResultCount = 800;
    private const int FixIndex = 81;
    private const int MinimumIndexGap = 24;
    private const int SnapShotCount = 2;

    private readonly ILogger<PointAccumulateForSGR9Worker> _logger;
    private readonly IHolderBalanceProvider _holderBalanceProvider;
    private readonly INESTRepository<PointsSnapshotIndex, string> _pointSnapshotIndexRepository;
    private readonly IOptionsMonitor<WorkerOptions> _workerOptionsMonitor;
    private readonly IOptionsMonitor<PointTradeOptions> _pointTradeOptions;

    private readonly IObjectMapper _objectMapper;
    private readonly IPointDailyRecordService _pointDailyRecordService;
    private readonly ISymbolDayPriceProvider _symbolDayPriceProvider;
    private readonly ISchrodingerCatProvider _schrodingerCatProvider;
    private readonly IDistributedCache<List<int>> _distributedCache;
    private readonly IPointDispatchProvider _pointDispatchProvider;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly string _lockKey = "PointAccumulateForSGR9Worker";

    public PointAccumulateForSGR9Worker(AbpAsyncTimer timer,IServiceScopeFactory serviceScopeFactory,ILogger<PointAccumulateForSGR9Worker> logger,
        IHolderBalanceProvider holderBalanceProvider, IOptionsMonitor<WorkerOptions> workerOptionsMonitor,
        INESTRepository<PointsSnapshotIndex, string> pointSnapshotIndexRepository, IObjectMapper objectMapper,
        IPointDailyRecordService pointDailyRecordService,
        ISymbolDayPriceProvider symbolDayPriceProvider,
        IDistributedCache<List<int>> distributedCache,
        IPointDispatchProvider pointDispatchProvider,
        IAbpDistributedLock distributedLock,
        IOptionsMonitor<PointTradeOptions> pointTradeOptions,
        ISchrodingerCatProvider schrodingerCatProvider): base(timer,
        serviceScopeFactory)
    {
        _logger = logger;
        _holderBalanceProvider = holderBalanceProvider;
        _workerOptionsMonitor = workerOptionsMonitor;
        _pointSnapshotIndexRepository = pointSnapshotIndexRepository;
        _objectMapper = objectMapper;
        _pointDailyRecordService = pointDailyRecordService;
        _symbolDayPriceProvider = symbolDayPriceProvider;
        _pointTradeOptions = pointTradeOptions;
        _distributedCache = distributedCache;
        _pointDispatchProvider = pointDispatchProvider;
        _distributedLock = distributedLock;
        _schrodingerCatProvider = schrodingerCatProvider;
        timer.Period = _workerOptionsMonitor.CurrentValue.GetWorkerPeriodMinutes(_lockKey) * 60 * 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        await using var handle = await _distributedLock.TryAcquireAsync(_lockKey);
        var openSwitch = _workerOptionsMonitor.CurrentValue.GetWorkerSwitch(_lockKey);
        _logger.LogInformation("PointAccumulateForSGR9Worker start openSwitch {openSwitch}", openSwitch);
        if (!openSwitch)
        {
            _logger.LogWarning("PointAccumulateForSGR9Worker has not open...");
            return;
        }
       
        var pointName = _workerOptionsMonitor.CurrentValue.GetWorkerPointName(_lockKey);
        
        var bizDate = _workerOptionsMonitor.CurrentValue.GetWorkerBizDate(_lockKey);
        if (bizDate.IsNullOrEmpty())
        {
            bizDate = DateTime.UtcNow.ToString(TimeHelper.Pattern);
        }

        await DoSyncHolderBalance(bizDate, pointName);    
        
        _logger.LogInformation("PointAccumulateForSGR9Worker end...");
    }
    
    private async Task DoSyncHolderBalance(string bizDate, string pointName)
    {
        _logger.LogInformation("PointAccumulateForSGR9Worker execute for bizDate: {bizDate} pointName:{1}", bizDate, pointName);
        
        var isExecuted = await _pointDispatchProvider.GetDispatchAsync(PointDispatchConstants.SYNC_SGR9_PREFIX , bizDate, pointName);
        if (isExecuted)
        {
            _logger.LogInformation("PointAccumulateForSGR9Worker has been executed for bizDate: {0} pointName:{1}", bizDate, pointName);
            return;
        }
        
        
        // var dateTime = await _distributedCache.GetAsync(PointDispatchConstants.UNISWAP_PRICE_PREFIX + TimeHelper.GetUtcDaySeconds());
        // if (dateTime == null)
        // {
        //     _logger.LogInformation("UniswapPriceSnapshotWorker has not executed today.");
        //     return;
        // }
        
        DateTime now = DateTime.Now;
        int curIndex = now.Hour * 6 + now.Minute / 10;
        var indexList = await _distributedCache.GetAsync(PointDispatchConstants.SGR9_SNAPSHOT_INDEX_CACHE_KEY_PREFIX + bizDate);
        if (indexList == null)
        {
            indexList = await SetSnapshotIndexCacheAsync(bizDate, curIndex);
        }
        
        _logger.LogInformation("PointAccumulateForSGR9Worker Index Judgement, {index1}, {index2}, {curIndex}", 
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
        
        var chainIds = _workerOptionsMonitor.CurrentValue.ChainIds;
        foreach (var chainId in chainIds)
        {
            await HandleHolderDailyChangeAsync(chainId, bizDate, pointName, indexList.IndexOf(curIndex));
        }
    }

    private async Task HandleHolderDailyChangeAsync(string chainId, string bizDate, string pointName, int snapshotIndex)
    {
        _logger.LogInformation("PointAccumulateForSGR9Worker Took Snapshot for date: {date}, index:{index}", bizDate, snapshotIndex);
        var skipCount = 0;
        List<SchrodingerIndexerDto> holderList;
        SchrodingerIndexerListDto getSGR1HolderResult;
        var priceBizDate = GetPriceBizDate(bizDate);
        var baseSymbol = _pointTradeOptions.CurrentValue.BaseCoin;
        do
        {
            var input = new GetCatListInput
            {
                ChainId = chainId,
                Generations = new List<int> { 0 },
                FilterSgr = false,
                SkipCount = skipCount,
                Keyword = baseSymbol,
                MaxResultCount = MaxResultCount
            };
            getSGR1HolderResult = await _schrodingerCatProvider.GetSchrodingerCatListAsync(input);
           
            if (getSGR1HolderResult == null)
            {
                _logger.LogError("GetSchrodingerCatListAsync result is null");
                break;
            }
            
            _logger.LogInformation("PointAccumulateForSGR9Worker GetSchrodingerCatListAsync, start: {start}, total: {total}", skipCount, getSGR1HolderResult.TotalCount);


            if (getSGR1HolderResult.Data.IsNullOrEmpty())
            {
                break;
            }

            holderList = getSGR1HolderResult.Data;
            var realHolders = holderList
                .Where(t => !_pointTradeOptions.CurrentValue.BlackPointAddressList.Contains(t.Address)).ToList();
            if (realHolders.IsNullOrEmpty())
            {
                continue;
            }

            var snapshots = _objectMapper.Map<List<SchrodingerIndexerDto>, List<PointsSnapshotIndex>>(realHolders);
            
            var now = DateTime.UtcNow;
            snapshots.ForEach(x =>
            {
                x.Id = IdGenerateHelper.GetId(x.Address, x.PointName, now.ToString("yyyy-MM-dd HH:mm"));
                x.PointName = pointName;
                x.BizDate = bizDate;
                x.CreateTime = now;
            });
            
            await _pointSnapshotIndexRepository.BulkAddOrUpdateAsync(snapshots);
            
            skipCount += getSGR1HolderResult.Data.Count;
        } while (!getSGR1HolderResult.Data.IsNullOrEmpty());
        
        _logger.LogInformation("PointAccumulateForSGR9Worker GetSchrodingerCatListAsync Finish");
        
        if (snapshotIndex == SnapShotCount-1)
        {
            _logger.LogInformation("PointAccumulateForSGR9Worker cal points");
            await Task.Delay(3000);
            var allSnapshots = await GetAllIndex(bizDate, pointName);
            _logger.LogInformation("PointAccumulateForSGR9Worker  snapshot counts: {cnt}", allSnapshots.Count);

            var snapshotByAddress = allSnapshots.GroupBy(snapshot => snapshot.Address).Select(group => new HolderDailyChangeDto
            {
                Address = group.Key,
                Balance = (long)group.Sum(item => item.Amount)/2,
                Symbol = baseSymbol,
                Date = bizDate
            });
            
            _logger.LogInformation("PointAccumulateForSGR9Worker  snapshot by address counts: {cnt}", snapshotByAddress.Count());
            
            var symbols = new List<string> { baseSymbol };
            var symbolPriceDict = await _symbolDayPriceProvider.GetSymbolPricesAsync(priceBizDate, symbols);
            // var symbolPrice = DecimalHelper.GetValueFromDict(symbolPriceDict, baseSymbol, baseSymbol);
            var symbolPrice = (decimal)2.2;
            
            foreach (var snapshot in snapshotByAddress)
            {
                var dayBefore = TimeHelper.GetDateStrAddDays(bizDate, -1);
                var excludeDate = new List<string> { dayBefore, bizDate };
                var lastHoldingRecord = await _holderBalanceProvider.GetLastHoldingRecordAsync(chainId, snapshot.Address, baseSymbol, excludeDate);
                if (lastHoldingRecord == null || lastHoldingRecord.Balance <= 0)
                {
                    _logger.LogInformation("PointAccumulateForSGR9Worker Holding Less Than 24hours, address: {address}", snapshot.Address);
                    continue;
                }
                
                await _pointDailyRecordService.HandlePointDailyChangeAsync(chainId, pointName, snapshot, symbolPrice);
            }
            
            await _pointDispatchProvider.SetDispatchAsync(PointDispatchConstants.SYNC_SGR9_PREFIX, bizDate,
                pointName, true);
        }

        _logger.LogInformation("PointAccumulateForSGR9Worker chainId:{chainId} end...", chainId);
    }

    private static string GetPriceBizDate(string bizDate)
    {
        string priceBizDate;
        if (bizDate.Equals(DateTime.UtcNow.ToString(TimeHelper.Pattern)))
        {
            priceBizDate = TimeHelper.GetDateStrAddDays(bizDate, -1);
        }
        else
        {
            priceBizDate = bizDate;
        }

        return priceBizDate;
    }
    
    private  async Task<List<PointsSnapshotIndex>> GetAllIndex(string bizDate, string pointName)
    {
        var res = new List<PointsSnapshotIndex>();
        List<PointsSnapshotIndex> list;
        var skipCount = 0;
        var mustQuery = new List<Func<QueryContainerDescriptor<PointsSnapshotIndex>, QueryContainer>>();
        
        mustQuery.Add(q => q.Term(i
            => i.Field(index => index.BizDate).Value(bizDate)));
        
        mustQuery.Add(q => q.Term(i
            => i.Field(index => index.PointName).Value(pointName)));


        QueryContainer Filter(QueryContainerDescriptor<PointsSnapshotIndex> f) =>
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
        _logger.LogInformation("PointAccumulateForSGR9Worker startIndex: {index1}", startIndex);
        AssertHelper.IsTrue(120 - startIndex > MinimumIndexGap, "PointAccumulateForSGR9Worker minimum gap cannot be satisfied");

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
        
        await _distributedCache.SetAsync(PointDispatchConstants.SGR9_SNAPSHOT_INDEX_CACHE_KEY_PREFIX + bizDate, data, new DistributedCacheEntryOptions()
        {
            SlidingExpiration = TimeSpan.FromDays(7)
        });
        
        _logger.LogInformation("PointAccumulateForSGR9Worker Generate Snapshot Index, {index1}, {index2}", randomNumber1, randomNumber2);
        
        return  data;
    }
}