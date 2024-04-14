using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SchrodingerServer.Common;
using SchrodingerServer.Common.HttpClient;
using SchrodingerServer.Options;
using SchrodingerServer.Point;

namespace SchrodingerServer.Token;

public class GateIoProvider : IExchangeProvider
{
    private readonly IOptionsMonitor<ExchangeOptions> _exchangeOptions;
    private readonly IHttpProvider _httpProvider;

    public GateIoProvider(IOptionsMonitor<ExchangeOptions> exchangeOptions, IHttpProvider httpProvider)
    {
        _exchangeOptions = exchangeOptions;
        _httpProvider = httpProvider;
    }

    public static class Api
    {
        public static readonly ApiInfo Candlesticks = new(HttpMethod.Get, "/api/v4/spot/candlesticks");
    }

    public ExchangeProviderName Name()
    {
        return ExchangeProviderName.GateIo;
    }

    public async Task<TokenExchangeDto> LatestAsync(string fromSymbol, string toSymbol)
    {

        if (fromSymbol == toSymbol)
        {
            return TokenExchangeDto.One(fromSymbol, toSymbol, DateTime.UtcNow.ToUtcMilliSeconds());
        }
        var resp = await _httpProvider.InvokeAsync<List<List<string>>>(_exchangeOptions.CurrentValue.GateIo.BaseUrl,
            Api.Candlesticks, param: new Dictionary<string, string>
            {
                ["currency_pair"] = string.Join(CommonConstant.Underline, fromSymbol, toSymbol),
                ["limit"] = "1",
                ["interval"] = Interval.Minute1
            });
        AssertHelper.NotEmpty(resp, "Empty result");
        var klineData = new CandlesticksResponse(resp[0]);
        return new TokenExchangeDto
        {
            FromSymbol = fromSymbol,
            ToSymbol = toSymbol,
            Timestamp = klineData.TimestampSeconds * 1000,
            Exchange = klineData.ClosingPrice
        };
    }

    public class CandlesticksResponse
    {
        public long TimestampSeconds { get; set; }
        public decimal TransactionAmount { get; set; }
        public decimal ClosingPrice { get; set; }
        public decimal HighestPrice { get; set; }
        public decimal LowestPrice { get; set; }
        public decimal OpeningPrice { get; set; }
        public decimal BaseCurrencyTradingVolume { get; set; }
        public bool WindowClosed { get; set; }

        public CandlesticksResponse()
        {
        }

        public CandlesticksResponse(List<string> vals)
        {
            TimestampSeconds = vals[0].SafeToLong();
            TransactionAmount = vals[1].SafeToDecimal();
            ClosingPrice = vals[2].SafeToDecimal();
            HighestPrice = vals[3].SafeToDecimal();
            LowestPrice = vals[4].SafeToDecimal();
            OpeningPrice = vals[5].SafeToDecimal();
            BaseCurrencyTradingVolume = vals[6].SafeToDecimal();
        }
    }


    public static class Interval
    {
        public static string Second10 = "10s";
        public static string Minute1 = "1m";
        public static string Minute5 = "5m";
        public static string Minute15 = "15m";
        public static string Minute30 = "30m";
        public static string Hour1 = "1h";
        public static string Hour4 = "4h";
        public static string Hour8 = "8h";
        public static string Day1 = "1d";
        public static string Day7 = "7d";
        public static string Day30 = "30d";
    }
}