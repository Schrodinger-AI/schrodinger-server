using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
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

namespace SchrodingerServer.EntityEventHandler.Core.Worker;


public class SyncHolderBalanceWorker :  AsyncPeriodicBackgroundWorkerBase
{
    private const int MaxResultCount = 800;

    private readonly ILogger<SyncHolderBalanceWorker> _logger;
    private readonly IHolderBalanceProvider _holderBalanceProvider;
    private readonly INESTRepository<HolderBalanceIndex, string> _holderBalanceIndexRepository;
    private readonly IOptionsMonitor<WorkerOptions> _workerOptionsMonitor;
    private readonly IOptionsMonitor<PointTradeOptions> _pointTradeOptions;

    private readonly IObjectMapper _objectMapper;
    private readonly IPointDailyRecordService _pointDailyRecordService;
    private readonly ISymbolDayPriceProvider _symbolDayPriceProvider;
    private readonly IDistributedCache<string> _distributedCache;
    private readonly IPointDispatchProvider _pointDispatchProvider;
    private readonly IAbpDistributedLock _distributedLock;
    private readonly string _lockKey = "ISyncHolderBalanceWorker";

    public SyncHolderBalanceWorker(AbpAsyncTimer timer,IServiceScopeFactory serviceScopeFactory,ILogger<SyncHolderBalanceWorker> logger,
        IHolderBalanceProvider holderBalanceProvider, IOptionsMonitor<WorkerOptions> workerOptionsMonitor,
        INESTRepository<HolderBalanceIndex, string> holderBalanceIndexRepository, IObjectMapper objectMapper,
        IPointDailyRecordService pointDailyRecordService,
        ISymbolDayPriceProvider symbolDayPriceProvider,
        IDistributedCache<string> distributedCache,
        IPointDispatchProvider pointDispatchProvider,
        IAbpDistributedLock distributedLock,
        IOptionsMonitor<PointTradeOptions> pointTradeOptions): base(timer,
        serviceScopeFactory)
    {
        _logger = logger;
        _holderBalanceProvider = holderBalanceProvider;
        _workerOptionsMonitor = workerOptionsMonitor;
        _holderBalanceIndexRepository = holderBalanceIndexRepository;
        _objectMapper = objectMapper;
        _pointDailyRecordService = pointDailyRecordService;
        _symbolDayPriceProvider = symbolDayPriceProvider;
        _pointTradeOptions = pointTradeOptions;
        _distributedCache = distributedCache;
        _pointDispatchProvider = pointDispatchProvider;
        _distributedLock = distributedLock;
        timer.Period = _workerOptionsMonitor.CurrentValue.GetWorkerPeriodMinutes(_lockKey) * 60 * 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        await using var handle = await _distributedLock.TryAcquireAsync(_lockKey);
        var openSwitch = _workerOptionsMonitor.CurrentValue.GetWorkerSwitch(_lockKey);
        _logger.LogInformation("SyncHolderBalanceWorker start openSwitch {openSwitch}", openSwitch);
        if (!openSwitch)
        {
            _logger.LogWarning("SyncHolderBalanceWorker has not open...");
            return;
        }
        //specify the point name to execute job
        // var txPointNames = _workerOptionsMonitor.CurrentValue.TxPointNames;
        
        var pointName = _workerOptionsMonitor.CurrentValue.GetWorkerPointName(_lockKey);
        
        var bizDateList = _workerOptionsMonitor.CurrentValue.GetWorkerBizDateList(_lockKey);
        if (!bizDateList.IsNullOrEmpty())
        {
            foreach (var bizDate in bizDateList)
            {
                await DoSyncHolderBalance(bizDate, pointName);
            }
        }
        else
        {
            var bizDate = _workerOptionsMonitor.CurrentValue.GetWorkerBizDate(_lockKey);
            if (bizDate.IsNullOrEmpty())
            {
                bizDate = DateTime.UtcNow.AddDays(-1).ToString(TimeHelper.Pattern);
            }

            await DoSyncHolderBalance(bizDate, pointName);
        }   
        
        _logger.LogInformation("SyncHolderBalanceWorker end...");
    }
    
    private async Task DoSyncHolderBalance(string bizDate, string pointName)
    {
        _logger.LogInformation("SyncHolderBalanceWorker execute for bizDate: {bizDate} pointName:{1}", bizDate, pointName);

        var isExecuted = await _pointDispatchProvider.GetDispatchAsync(PointDispatchConstants.SYNC_HOLDER_BALANCE_PREFIX , bizDate, pointName);
        if (isExecuted)
        {
            _logger.LogInformation("SyncHolderBalanceWorker has been executed for bizDate: {0} pointName:{1}", bizDate, pointName);
            return;
        }
        
        var dateTime = await _distributedCache.GetAsync(PointDispatchConstants.UNISWAP_PRICE_PREFIX + TimeHelper.GetUtcDaySeconds());
        if (dateTime == null)
        {
            _logger.LogInformation("UniswapPriceSnapshotWorker has not executed today.");
            return;
        }
        var chainIds = _workerOptionsMonitor.CurrentValue.ChainIds;
        foreach (var chainId in chainIds)
        {
            await HandleHolderDailyChangeAsync(chainId, bizDate, pointName);
            await Task.Delay(5000);
            await HandleHolderBalanceNoChangesAsync(chainId, bizDate, pointName);
        }

        await _pointDispatchProvider.SetDispatchAsync(PointDispatchConstants.SYNC_HOLDER_BALANCE_PREFIX, bizDate,
            pointName, true);
    }

    private async Task HandleHolderDailyChangeAsync(string chainId, string bizDate, string pointName)
    {
        _logger.LogInformation("HandleHolderDailyChangeAsync start...");
        var skipCount = 0;
        List<HolderDailyChangeDto> dailyChanges;
        var priceBizDate = GetPriceBizDate(bizDate);
        do
        {
            dailyChanges =
                await _holderBalanceProvider.GetHolderDailyChangeListAsync(chainId, bizDate, skipCount, MaxResultCount, _pointTradeOptions.CurrentValue.BaseCoin);
            _logger.LogInformation(
                "GetHolderDailyChangeList chainId:{chainId} skipCount: {skipCount} bizDate:{bizDate} count: {count}",
                chainId, skipCount,bizDate , dailyChanges?.Count);
            if (dailyChanges.IsNullOrEmpty())
            {
                break;
            }
            var  realDailyChanges = dailyChanges
                .Where(t => !_pointTradeOptions.CurrentValue.BlackPointAddressList.Contains(t.Address)).ToList();
            if (realDailyChanges.IsNullOrEmpty())
            {
              continue;
            }

            var symbols = realDailyChanges.Select(item => item.Symbol).ToHashSet();
            symbols.Add(_pointTradeOptions.CurrentValue.BaseCoin);

            var symbolPriceDict = await _symbolDayPriceProvider.GetSymbolPricesAsync(priceBizDate, symbols.ToList());

            //get user latest date balance and add change
            var saveList = new List<HolderBalanceIndex>();
            foreach (var item in realDailyChanges)
            {
                var symbolPrice = DecimalHelper.GetValueFromDict(symbolPriceDict, item.Symbol,
                    _pointTradeOptions.CurrentValue.BaseCoin);
                var holderBalance = _objectMapper.Map<HolderDailyChangeDto, HolderBalanceIndex>(item);
                holderBalance.ChainId = chainId;
                holderBalance.Id = IdGenerateHelper.GetHolderBalanceId(chainId, holderBalance.Symbol, holderBalance.Address);
                await _pointDailyRecordService.HandlePointDailyChangeAsync(chainId, pointName, item, symbolPrice);
                saveList.Add(holderBalance);
            }

            //update bizDate holder balance
            await _holderBalanceIndexRepository.BulkAddOrUpdateAsync(saveList);           
            skipCount += dailyChanges.Count;
            await Task.Delay(200);
        } while (!dailyChanges.IsNullOrEmpty());

        _logger.LogInformation("SyncHolderBalanceWorker chainId:{chainId} end...", chainId);
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

    private async Task HandleHolderBalanceNoChangesAsync(string chainId, string bizDate, string pointName)
    {
        var skipCount = 0;
        List<HolderBalanceIndex> holderBalanceIndices;

        var priceBizDate = GetPriceBizDate(bizDate);
        do
        {
            holderBalanceIndices = await _holderBalanceProvider.GetPreHolderBalanceListAsync(chainId, bizDate,
                skipCount, MaxResultCount);
            _logger.LogInformation(
                "GetHolderDailyChangeList before  chainId:{chainId} skipCount: {skipCount} bizDate:{bizDate} count: {count}",
                chainId, skipCount,bizDate , holderBalanceIndices?.Count);
            var  realHolderBalanceIndices = holderBalanceIndices
                .Where(t => !_pointTradeOptions.CurrentValue.BlackPointAddressList.Contains(t.Address)).ToList();
            if (realHolderBalanceIndices.IsNullOrEmpty())
            {
                continue;
            }

            var symbols = realHolderBalanceIndices.Select(item => item.Symbol).ToHashSet();
            symbols.Add(_pointTradeOptions.CurrentValue.BaseCoin);
            var symbolPriceDict = await _symbolDayPriceProvider.GetSymbolPricesAsync(priceBizDate, symbols.ToList());

            foreach (var item in realHolderBalanceIndices)
            {
                var symbolPrice = DecimalHelper.GetValueFromDict(symbolPriceDict, item.Symbol,
                    _pointTradeOptions.CurrentValue.BaseCoin);

                var dto = new HolderDailyChangeDto
                {
                    Address = item.Address,
                    Date = bizDate,
                    Symbol = item.Symbol,
                    Balance = item.Balance
                };
                await _pointDailyRecordService.HandlePointDailyChangeAsync(chainId, pointName, dto, symbolPrice);
            }

            skipCount += holderBalanceIndices.Count;
        } while (!holderBalanceIndices.IsNullOrEmpty());
    }
}