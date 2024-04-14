using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SchrodingerServer.Common;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Background.Providers;

public interface IZealyClientProvider
{
    Task<T> GetAsync<T>(string url);
}

public class ZealyClientProvider : IZealyClientProvider, ISingletonDependency
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ZealyClientProvider> _logger;

    public ZealyClientProvider(ILogger<ZealyClientProvider> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<T> GetAsync<T>(string url)
    {
        var client = _httpClientFactory.CreateClient(CommonConstant.ZealyClientName);
        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (response.StatusCode != HttpStatusCode.OK)
        {
            _logger.LogError("response from zealy status code not success, code:{code}, message: {message}, url:{url}",
                response.StatusCode, content, url);

            throw new UserFriendlyException(content, ((int)response.StatusCode).ToString());
        }

        return JsonConvert.DeserializeObject<T>(content);
    }
}