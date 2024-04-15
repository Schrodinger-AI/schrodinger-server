using System.Collections.Generic;

namespace SchrodingerServer.CoinGeckoApi
{
    public class CoinGeckoOptions
    {
        public string BaseUrl { get; set; }
        public Dictionary<string, string> CoinIdMapping { get; set; }
        public string ApiKey { get; set; }
    }
}