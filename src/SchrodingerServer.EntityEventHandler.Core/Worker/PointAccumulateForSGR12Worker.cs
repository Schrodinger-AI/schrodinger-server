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
using Orleans;
using SchrodingerServer.Cat.Provider;
using SchrodingerServer.Cat.Provider.Dtos;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Dtos.Cat;
using SchrodingerServer.EntityEventHandler.Core.Options;
using SchrodingerServer.Grains.Grain.Points;
using SchrodingerServer.Points.Provider;
using SchrodingerServer.Users;
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


public class PointAccumulateForSGR12Worker :  AsyncPeriodicBackgroundWorkerBase
{
    private const int MaxResultCount = 500;
    private const int MinimumIndexGap = 24;
    private const int SnapShotCount = 2;
    private const string pointName = "XPSGR-12";

    private readonly ILogger<PointAccumulateForSGR12Worker> _logger;
    private readonly IOptionsMonitor<WorkerOptions> _workerOptionsMonitor;
    private readonly IOptionsMonitor<PointTradeOptions> _pointTradeOptions;
    private readonly INESTRepository<PointsSnapshotIndex, string> _pointSnapshotIndexRepository;
    
    private readonly IHolderBalanceProvider _holderBalanceProvider;
    private readonly IPointDispatchProvider _pointDispatchProvider;
    private readonly ISchrodingerCatProvider _schrodingerCatProvider;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly IDistributedCache<List<int>> _distributedCache;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IClusterClient _clusterClient;
    private readonly IObjectMapper _objectMapper;
    private readonly string _lockKey = "PointAccumulateForSGR12Worker";

    public PointAccumulateForSGR12Worker(AbpAsyncTimer timer,IServiceScopeFactory serviceScopeFactory,ILogger<PointAccumulateForSGR12Worker> logger,
        IOptionsMonitor<WorkerOptions> workerOptionsMonitor,
        IClusterClient clusterClient,
        IDistributedCache<List<int>> distributedCache,
        IHolderBalanceProvider holderBalanceProvider,
        IAbpDistributedLock distributedLock,
        IOptionsMonitor<PointTradeOptions> pointTradeOptions,
        IDistributedEventBus distributedEventBus,
        IPointDispatchProvider pointDispatchProvider,
        ISchrodingerCatProvider schrodingerCatProvider,
        IObjectMapper objectMapper,
        INESTRepository<PointsSnapshotIndex, string> pointSnapshotIndexRepository): base(timer,
        serviceScopeFactory)
    {
        _logger = logger;
        _workerOptionsMonitor = workerOptionsMonitor;
        _clusterClient = clusterClient;
        _pointTradeOptions = pointTradeOptions;
        _distributedLock = distributedLock;
        _holderBalanceProvider = holderBalanceProvider;
        _distributedCache = distributedCache;
        _pointSnapshotIndexRepository = pointSnapshotIndexRepository;
        _distributedEventBus = distributedEventBus;
        _pointDispatchProvider = pointDispatchProvider;
        _schrodingerCatProvider = schrodingerCatProvider;
        _objectMapper = objectMapper;
        timer.Period = _workerOptionsMonitor.CurrentValue.GetWorkerPeriodMinutes(_lockKey) * 60 * 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        await using var handle = await _distributedLock.TryAcquireAsync(_lockKey);
        var openSwitch = _workerOptionsMonitor.CurrentValue.GetWorkerSwitch(_lockKey);
        _logger.LogInformation("PointAccumulateForSGR12Worker start openSwitch {openSwitch}", openSwitch);
        if (!openSwitch)
        {
            return;
        }
      
        var bizDate = DateTime.UtcNow.ToString(TimeHelper.Pattern);
        await SGR12SnapshotWorkAsync(bizDate);
        
    }
    
    
    private async Task SGR12SnapshotWorkAsync(string bizDate)
    {
        _logger.LogInformation("PointAccumulateForSGR12Worker  date:{date} begin...", bizDate);
        
        DateTime now = DateTime.Now;
        int curIndex = now.Hour * 6 + now.Minute / 10;
        var indexList = await _distributedCache.GetAsync(PointDispatchConstants.SGR12_SNAPSHOT_INDEX_CACHE_KEY_PREFIX + bizDate);
        if (indexList == null)
        {
            indexList = await SetSnapshotIndexCacheAsync(bizDate, curIndex);
        }
        
        _logger.LogInformation("PointAccumulateForSGR12Worker Index Judgement, {index1}, {index2}, {curIndex}", 
            indexList[0], indexList[1], curIndex);

        var fixedIndexList = _workerOptionsMonitor.CurrentValue.GetTriggerIndexList(_lockKey);
        if (!fixedIndexList.IsNullOrEmpty())
        {
            indexList = fixedIndexList.ToList();
            _logger.LogInformation("PointAccumulateForSGR12Worker change snap index list to {index1}", indexList);
        }
        
        if (!indexList.Contains(curIndex))
        {
            return;
        }
        
        var chainId = _workerOptionsMonitor.CurrentValue.ChainIds.FirstOrDefault();
        _logger.LogInformation("PointAccumulateForSGR12Worker CreateSnapshotAsync");
        await CreateSnapshotAsync(chainId, bizDate);
        _logger.LogInformation("PointAccumulateForSGR12Worker CreateSnapshotAsync Finish");
        
        if (indexList.IndexOf(curIndex) != SnapShotCount - 1)
        {
            return;
        }
        
        _logger.LogInformation("PointAccumulateForSGR12Worker cal points");
        await Task.Delay(3000);
        var allSnapshots = await GetAllIndex(bizDate);
        _logger.LogInformation("PointAccumulateForSGR12Worker snapshot counts: {cnt}", allSnapshots.Count);

        var snapshotByAddress = allSnapshots.GroupBy(snapshot => snapshot.Address).Select(group => new HolderDailyChangeDto
        {
            Address = group.Key,
            Balance = (long)group.Sum(item => item.Amount)/2,
            Date = bizDate
        }).ToList();
        _logger.LogInformation("PointAccumulateForSGR12Worker  snapshot by address counts: {cnt}", snapshotByAddress.Count);
        
        foreach (var snapshot in snapshotByAddress)
        {
            var id = IdGenerateHelper.GetId(bizDate, pointName, snapshot.Address);
                
            var input = new PointDailyRecordGrainDto()
            {
                Id = id,
                ChainId = chainId,
                PointName = pointName,
                BizDate = bizDate,
                Address = snapshot.Address,
                HolderBalanceId = IdGenerateHelper.GetHolderBalanceId(chainId, "", snapshot.Address),
                PointAmount = snapshot.Balance
            };
      
            var pointDailyRecordGrain = _clusterClient.GetGrain<IPointDailyRecordGrain>(input.Id);
            var result = await pointDailyRecordGrain.UpdateAsync(input);
            _logger.LogDebug("PointAccumulateForSGR12Worker write grain result: {result}, record: {record}", 
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
                PointAmount = snapshot.Balance
            };
                
            await  _distributedEventBus.PublishAsync(pointDailyRecordEto);
        }
        
        _logger.LogInformation("PointAccumulateForSGR12Worker end...");
    }
    
    private async Task CreateSnapshotAsync(string chainId, string bizDate)
    {
        var skipCount = 0;
        List<SchrodingerIndexerDto> holderList;
        SchrodingerIndexerListDto getCatHolderResult;
        do
        {
            var input = new GetCatListInput
            {
                ChainId = chainId,
                FilterSgr = true,
                SkipCount = skipCount,
                MaxResultCount = MaxResultCount,
            };
            getCatHolderResult = await _schrodingerCatProvider.GetSchrodingerCatListAsync(input);
           
            if (getCatHolderResult == null)
            {
                _logger.LogError("GetSchrodingerCatListAsync result is null");
                break;
            }
            
            _logger.LogInformation("PointAccumulateForSGR12Worker GetSchrodingerCatListAsync, start: {start}, total: {total}", skipCount, getCatHolderResult.TotalCount);


            if (getCatHolderResult.Data.IsNullOrEmpty())
            {
                break;
            }

            holderList = getCatHolderResult.Data;
            var realHolders = holderList
                .Where(t => !_pointTradeOptions.CurrentValue.BlackPointAddressList.Contains(t.Address)).ToList();
            if (realHolders.IsNullOrEmpty())
            {
                continue;
            }
            _logger.LogInformation("PointAccumulateForSGR12Worker real Holders cnt:{total}", realHolders.Count);

            var now = DateTime.UtcNow;
            var validHolders = new List<PointsSnapshotIndex>();
            foreach (var holderInfo in realHolders)
            {
                var dayBefore = TimeHelper.GetDateStrAddDays(bizDate, -1);
                var excludeDate = new List<string> { dayBefore, bizDate };
                var lastHoldingRecord = await _holderBalanceProvider.GetLastHoldingRecordAsync(chainId, holderInfo.Address, holderInfo.Symbol, excludeDate);
                if (lastHoldingRecord == null || lastHoldingRecord.Balance <= 0)
                {
                    _logger.LogInformation("PointAccumulateForSGR12Worker Holding Cat Less Than 24hours, address: {address}", holderInfo.Address);
                    continue;
                }
                
                var snapshot = _objectMapper.Map<SchrodingerIndexerDto, PointsSnapshotIndex>(holderInfo);   
                var pointBySymbolDto = await _schrodingerCatProvider.GetHoldingPointBySymbol(holderInfo.Symbol, chainId);
                snapshot.Amount = pointBySymbolDto.Point * holderInfo.Amount;
                snapshot.Id = IdGenerateHelper.GetId(holderInfo.Address, holderInfo.Symbol, pointName, now.ToString("yyyy-MM-dd HH:mm"));
                snapshot.PointName = pointName;
                snapshot.BizDate = bizDate;
                snapshot.CreateTime = now;
                validHolders.Add(snapshot);
            }
            
            _logger.LogInformation("PointAccumulateForSGR12Worker valid Holders cnt:{total}", validHolders.Count);
            
            await _pointSnapshotIndexRepository.BulkAddOrUpdateAsync(validHolders);
            
            skipCount += getCatHolderResult.Data.Count;
        } while (!getCatHolderResult.Data.IsNullOrEmpty());
    }
    
    private  async Task<List<PointsSnapshotIndex>> GetAllIndex(string bizDate)
    {
        var res = new List<PointsSnapshotIndex>();
        List<PointsSnapshotIndex> list;
        var skipCount = 0;
        var mustQuery = new List<Func<QueryContainerDescriptor<PointsSnapshotIndex>, QueryContainer>>
        {
            q => q.Term(i
                => i.Field(index => index.BizDate).Value(bizDate)),
            q => q.Term(i
                => i.Field(index => index.PointName).Value(pointName)),
        };
        
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
        _logger.LogInformation("PointAccumulateForSGR12Worker startIndex: {index1}", startIndex);
        AssertHelper.IsTrue(120 - startIndex > MinimumIndexGap, "PointAccumulateForSGR12Worker minimum gap cannot be satisfied");

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
        
        await _distributedCache.SetAsync(PointDispatchConstants.SGR12_SNAPSHOT_INDEX_CACHE_KEY_PREFIX + bizDate, data, new DistributedCacheEntryOptions()
        {
            SlidingExpiration = TimeSpan.FromDays(7)
        });
        
        _logger.LogInformation("PointAccumulateForSGR12Worker Generate Snapshot Index, {index1}, {index2}", randomNumber1, randomNumber2);
        
        return  data;
    }
}