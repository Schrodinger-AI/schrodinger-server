using System;
using System.Net.Http;
using System.Threading.Tasks;
using CoinGecko.Clients;
using CoinGecko.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SchrodingerServer.CoinGeckoApi;
using SchrodingerServer.Common;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Token;

public class TokenPriceProvider : ITokenPriceProvider, ISingletonDependency
{
    private readonly ICoinGeckoClient _coinGeckoClient;
    private readonly IRequestLimitProvider _requestLimitProvider;
    private readonly IOptionsMonitor<CoinGeckoOptions> _coinGeckoOptions;
    private readonly IDistributedCache<PriceCacheItem> _distributedCache;

    private const string UsdSymbol = "usd";
    private const string PriceCachePrefix = "usd";
    private const int PriceCacheTimeout = 60;

    public ILogger<TokenPriceProvider> Logger { get; set; }

    public TokenPriceProvider(IRequestLimitProvider requestLimitProvider, IHttpClientFactory httpClientFactory, IOptionsMonitor<CoinGeckoOptions> options, IDistributedCache<PriceCacheItem> distributedCache)
    {
        _requestLimitProvider = requestLimitProvider;
        _coinGeckoOptions = options;
        _distributedCache = distributedCache;
        Logger = NullLogger<TokenPriceProvider>.Instance;
        if (_coinGeckoOptions.CurrentValue.ApiKey.IsNullOrEmpty())
        {
            _coinGeckoClient = CoinGeckoClient.Instance;
            return;
        }

        _coinGeckoClient = new CoinGeckoClient(InitCoinGeckoClient(httpClientFactory));
    }

    private HttpClient InitCoinGeckoClient(IHttpClientFactory httpClientFactory)
    {
        var httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        if (_coinGeckoOptions.CurrentValue.BaseUrl.NotNullOrEmpty())
        {
            httpClient.BaseAddress = new Uri(_coinGeckoOptions.CurrentValue.BaseUrl);
        }

        if ((_coinGeckoOptions.CurrentValue.BaseUrl ?? "").Contains("pro"))
        {
            httpClient.DefaultRequestHeaders.Add("x-cg-pro-api-key", _coinGeckoOptions.CurrentValue.ApiKey);
        }

        return httpClient;
    }

    public async Task<decimal> GetPriceByCacheAsync(string symbol)
    {
        var priceItem = await _distributedCache.GetOrAddAsync(
            string.Join(":", PriceCachePrefix, symbol),
            async () => new PriceCacheItem
            {
                Price = await GetPriceAsync(symbol)
            } ,
            () => new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(PriceCacheTimeout)
            }
        );
        return priceItem.Price;
    }
    
    public async Task<decimal> GetPriceAsync(string symbol)
    {
        if (string.IsNullOrEmpty(symbol))
        {
            return 0;
        }

        var coinId = GetCoinIdAsync(symbol);
        if (coinId == null)
        {
            Logger.LogWarning($"can not get the token {symbol}");
            return 0;
        }
        try
        {
            var coinData =
                await RequestAsync(async () =>
                    await _coinGeckoClient.SimpleClient.GetSimplePrice(new[] { coinId }, new[] { UsdSymbol }));

            if (!coinData.TryGetValue(coinId, out var value))
            {
                return 0;
            }

            return value[UsdSymbol].Value;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"can not get current price :{symbol}.");
            return 0;
        }
    }

    public async Task<decimal> GetHistoryPriceAsync(string symbol, DateTime dateTime)
    {
        if (string.IsNullOrEmpty(symbol))
        {
            return 0;
        }

        var coinId = GetCoinIdAsync(symbol);
        if (coinId == null)
        {
            Logger.LogWarning($"can not get the token {symbol}");
            return 0;
        }

        try
        {
            var coinData =
                await RequestAsync(async () => await _coinGeckoClient.CoinsClient.GetHistoryByCoinId(coinId,
                    dateTime.ToString("dd-MM-yyyy"), "false"));

            if (coinData.MarketData == null)
            {
                return 0;
            }

            return (decimal)coinData.MarketData.CurrentPrice[UsdSymbol].Value;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"can not get :{symbol} price.");
            return 0;
        }
    }

    private string GetCoinIdAsync(string symbol)
    {
        return _coinGeckoOptions.CurrentValue.CoinIdMapping.TryGetValue(symbol.ToUpper(), out var id) ? id : null;
    }

    private async Task<T> RequestAsync<T>(Func<Task<T>> task)
    {
        await _requestLimitProvider.RecordRequestAsync();
        return await task();
    }
}