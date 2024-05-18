using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using SchrodingerServer.Common.HttpClient;
using SchrodingerServer.Dtos.Uniswap;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Common;

public interface IEtherscanProvider 
{
    Task<string> GetBlockNoByTimeAsync(long ts);
}


public class EtherscanProvider : IEtherscanProvider, ISingletonDependency
{
    private const string Uri = "https://api.etherscan.io/api";
    private const string ApiKey = "31TS3UVYZIHWP556AWECT32EBSXPYA2UGC";
    
    private readonly IHttpProvider _httpProvider;

    public EtherscanProvider(
        IHttpProvider httpProvider)
    {
        _httpProvider = httpProvider;
    }
    
    public async Task<string> GetBlockNoByTimeAsync(long ts)
    {
        var resp = await _httpProvider.InvokeAsync<GetBlockNoDto>(HttpMethod.Get,
            Uri,
            param: new Dictionary<string, string>
            {
                ["module"] = "block",
                ["action"] = "getblocknobytime",
                ["timestamp"] = ts.ToString(),
                ["closest"] = "before",
                ["apikey"] = ApiKey
            });
         
        AssertHelper.IsTrue(resp?.Status == "1", "request getblocknobytime error");

        return resp?.Result;
    }
}