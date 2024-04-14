using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Background.Providers;

public interface IZealyClientProxyProvider
{
    Task<T> GetAsync<T>(string url);
}

public class ZealyClientProxyProvider : IZealyClientProxyProvider, ISingletonDependency
{
    private readonly IZealyClientProvider _zealyClientProvider;
    private readonly IZealyRateLimitProvider _zealyRateLimitProvider;

    public ZealyClientProxyProvider(IZealyClientProvider zealyClientProvider,
        IZealyRateLimitProvider zealyRateLimitProvider)
    {
        _zealyClientProvider = zealyClientProvider;
        _zealyRateLimitProvider = zealyRateLimitProvider;
    }

    public async Task<T> GetAsync<T>(string url)
    {
        var isLimit = await _zealyRateLimitProvider.AddOneAsync();
        if (!isLimit)
        {
            return await _zealyClientProvider.GetAsync<T>(url);
        }

        await Task.Delay(800);
        return await GetAsync<T>(url);
    }
}