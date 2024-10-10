using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Options;
using SchrodingerServer.Symbol;
using SchrodingerServer.Token;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Caching;
using Volo.Abp.Threading;

namespace SchrodingerServer.Background.Workers;

public class UniswapPriceSnapshotWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly ILogger<UniswapPriceSnapshotWorker> _logger;
    private readonly IXgrPriceService _xgrPriceService;
    private readonly UniswapV3Provider _uniSwapV3Provider;
    private readonly IDistributedCache<string> _distributedCache;
    private readonly IOptionsMonitor<ExchangeOptions> _exchangeOptions;
    public UniswapPriceSnapshotWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
         IOptionsSnapshot<ZealyUserOptions> options,
        ILogger<UniswapPriceSnapshotWorker> logger,
        IXgrPriceService xgrPriceService,
        UniswapV3Provider uniSwapV3Provider,
        IOptionsMonitor<ExchangeOptions> exchangeOptions,
        IDistributedCache<string> distributedCache) : base(timer,
        serviceScopeFactory)
    {
        _logger = logger;
        _xgrPriceService = xgrPriceService;
        timer.Period = options.Value.Period  * 1000;
        _uniSwapV3Provider = uniSwapV3Provider;
        _distributedCache = distributedCache;
        _exchangeOptions = exchangeOptions;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        if (!_exchangeOptions.CurrentValue.UseUniswap)
        {
            return;
        }
        _logger.LogInformation("begin execute UniswapPriceSnapshotWorker.");
        var date = TimeHelper.GetUtcDaySeconds();
        var dateTime = await _distributedCache.GetAsync(PointDispatchConstants.UNISWAP_PRICE_PREFIX + date);
        if (dateTime != null)
        {
            _logger.LogInformation("UniswapPriceSnapshotWorker has been executed today.");
            return;
        }
        var tokenRes = await _uniSwapV3Provider.GetLatestUSDPriceAsync(date);
        if (tokenRes != null)
        {
            await _xgrPriceService.SaveXgrDayPriceAsync(true);
            await _distributedCache.SetAsync(PointDispatchConstants.UNISWAP_PRICE_PREFIX + date, DateTime.UtcNow.ToUtcSeconds().ToString(),  new DistributedCacheEntryOptions()
            {
                SlidingExpiration = TimeSpan.FromDays(2)
            });
        }
        _logger.LogInformation("finish execute UniswapPriceSnapshotWorker.");
    }
    
    private DateTime GetUtcDay()
    {
        DateTime nowUtc = DateTime.UtcNow;
        return new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc);
    }
}