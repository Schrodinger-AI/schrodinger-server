using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchrodingerServer.Common;
using SchrodingerServer.Config;
using SchrodingerServer.Options;
using SchrodingerServer.Symbol.Index;
using SchrodingerServer.Symbol.Provider;
using SchrodingerServer.Token;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Symbol;


public interface IXgrPriceService
{
    Task SaveXgrDayPriceAsync(bool isGen0);
    
    Task SaveUniqueXgrDayPriceAsync(bool isGen0);
}


public class XgrPriceService : IXgrPriceService,ISingletonDependency
{
    private readonly ISchrodingerSymbolProvider _schrodingerSymbolProvider;
    private readonly ISymbolPriceGraphProvider _symbolPriceGraphProvider;
    private readonly ITokenPriceProvider _tokenPriceProvider;
    private readonly IConfigAppService _configAppService;
    private readonly UniswapV3Provider _uniswapV3Provider;
    private readonly ISymbolDayPriceProvider _symbolDayPriceProvider;
    private readonly ILogger<XgrPriceService> _logger;
    private readonly IOptionsMonitor<ExchangeOptions> _exchangeOptions;
    private readonly IExchangeProvider _exchangeProvider;
    private readonly IDistributedCache<string> _distributedCache;
    private const int QueryOnceLimit = 100;
    private readonly string _dateTimeFormat = "yyyyMMdd";
    
    public XgrPriceService( ISchrodingerSymbolProvider schrodingerSymbolProvider,
        ISymbolPriceGraphProvider symbolPriceGraphProvider,
        ITokenPriceProvider tokenPriceProvider,
        IConfigAppService configAppService,
        UniswapV3Provider uniswapV3Provider,
        ISymbolDayPriceProvider symbolDayPriceProvider,
        IOptionsMonitor<ExchangeOptions> exchangeOptions,
        IExchangeProvider exchangeProvider,
        IDistributedCache<string> distributedCache,
        ILogger<XgrPriceService> logger)
    {
        _schrodingerSymbolProvider = schrodingerSymbolProvider;
        _symbolPriceGraphProvider = symbolPriceGraphProvider;
        _tokenPriceProvider = tokenPriceProvider;
        _configAppService = configAppService;
        _uniswapV3Provider = uniswapV3Provider;
        _symbolDayPriceProvider = symbolDayPriceProvider;
        _exchangeOptions = exchangeOptions;
        _logger = logger;
        _exchangeProvider = exchangeProvider;
        _distributedCache = distributedCache;
    }

    public async Task SaveXgrDayPriceAsync(bool isGen0)
    {
        var skipCount = 0;
        var date = getUTCDay();
        var dateStr = date.AddDays(-1).ToString(_dateTimeFormat);
        while (true)
        {
            var schrodingerSymbolList =
                await _schrodingerSymbolProvider.GetSchrodingerSymbolList(skipCount, QueryOnceLimit);
            if (schrodingerSymbolList.IsNullOrEmpty()) break;
            skipCount += QueryOnceLimit;
            List<SymbolDayPriceIndex> symbolDayPriceIndexList = new List<SymbolDayPriceIndex>();
            foreach (var item in schrodingerSymbolList)
            {
                var price = await GetSymbolPrice(item.Symbol,date.ToUtcSeconds(),isGen0);
                if (price > 0)
                {
                    var symbolDayPriceIndex = new SymbolDayPriceIndex()
                    {
                        Id = $"{item.Symbol}-{dateStr}",
                        Symbol = item.Symbol,
                        Price = price,
                        Date = dateStr
                    };
                    symbolDayPriceIndexList.Add(symbolDayPriceIndex);
                }
            }
            if (symbolDayPriceIndexList.Count > 0)
            {
                await _symbolDayPriceProvider.SaveSymbolDayPriceIndex(symbolDayPriceIndexList);
            }
            _logger.LogInformation("SaveXgrDayPriceAsync date:{date} isGen0:{isGen0} count:{count}", dateStr,isGen0,symbolDayPriceIndexList.Count);
        }
       
    }

    public async Task SaveUniqueXgrDayPriceAsync(bool isGen0)
    {
        var date = TimeHelper.GetUtcDaySeconds();
        var dateTime = await _distributedCache.GetAsync(PointDispatchConstants.UNISWAP_PRICE_PREFIX + date);
        if (dateTime != null)
        {
            _logger.LogInformation("UniswapPriceSnapshotWorker has been executed today.");
            return;
        }

        await SaveXgrDayPriceAsync(isGen0);
        await _distributedCache.SetAsync(PointDispatchConstants.UNISWAP_PRICE_PREFIX + date, DateTime.UtcNow.ToUtcSeconds().ToString(),  new DistributedCacheEntryOptions()
        {
            SlidingExpiration = TimeSpan.FromDays(2)
        });
    }

    private DateTime getUTCDay()
    {
        DateTime nowUtc = DateTime.UtcNow;
        return new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    private async Task<decimal> GetSymbolPrice(string symbol,long date,bool isGen0)
    {
        var getMyNftListingsDto = new GetNFTListingsDto()
        {
            ChainId = _configAppService.GetConfig()["curChain"],
            Symbol = symbol,
            SkipCount = 0,
            MaxResultCount = 1
        };
        decimal usdPrice = 0;
        try
        {
            bool isGen0Symbol  = GetIsGen0FromSymbol(symbol);
            if (isGen0 && isGen0Symbol)
            {
                if (_exchangeOptions.CurrentValue.UseUniswap)
                {
                    var tokenResponse  = await _uniswapV3Provider.GetLatestUSDPriceAsync(date);
                    if (tokenResponse != null )
                    {
                        usdPrice = Convert.ToDecimal(tokenResponse.PriceUSD);
                    }
                }
                else
                {
                    var gateIo = _exchangeOptions.CurrentValue.GateIo;
                    var tokenExchange  = await _exchangeProvider.LatestAsync(gateIo.FromSymbol,gateIo.ToSymbol);
                    if (tokenExchange != null)
                    {
                        var symbolUsdPrice = await _tokenPriceProvider.GetPriceByCacheAsync(gateIo.ToSymbol);
                        usdPrice = tokenExchange.Exchange * symbolUsdPrice;
                    }
                }

            }else if(!isGen0 && !isGen0Symbol)
            {
                var listingDto = await _symbolPriceGraphProvider.GetNFTListingsAsync(getMyNftListingsDto);
                if (listingDto != null && listingDto.TotalCount > 0)
                {
                    var tokenPrice = listingDto.Items[0].Prices;
                    var symbolUsdPrice = await _tokenPriceProvider.GetPriceByCacheAsync(listingDto.Items[0].PurchaseToken.Symbol);
                    usdPrice = tokenPrice* symbolUsdPrice;
                }
            }
            return  usdPrice;
        }catch (Exception e)
        {
            _logger.LogError(e, "GetSymbolPrice error symbol:{symbol} date {date}", symbol,date);
        }
        return 0;
    }
    
    public static bool GetIsGen0FromSymbol(string symbol)
    {
        try
        {
            return Convert.ToInt32(symbol.Split(CommonConstant.Separator)[1]) == 1;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

public class GetNFTListingsDto
{
    public string ChainId { get; set; }
    public string Symbol { get; set; }
    public int SkipCount { get; set; }
    
    public int MaxResultCount { get; set; }
}



