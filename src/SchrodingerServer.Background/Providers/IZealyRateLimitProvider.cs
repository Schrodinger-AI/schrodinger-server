using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Background.Providers;

public interface IZealyRateLimitProvider
{
    Task<bool> AddOneAsync();
}

public class ZealyRateLimitProvider : IZealyRateLimitProvider, ISingletonDependency
{
    private readonly IDistributedCache<string> _distributedCache;
    private readonly ILogger<ZealyRateLimitProvider> _logger;

    // single service, Multiple service instances need to use distributeLock.
    private SemaphoreSlim _asyncLock = new SemaphoreSlim(1);

    private const string ZealyRateLimitKey = "ZealyRateLimit";

    public ZealyRateLimitProvider(IDistributedCache<string> distributedCache, ILogger<ZealyRateLimitProvider> logger)
    {
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public async Task<bool> AddOneAsync()
    {
        var requestCount = await GetRequestCountAsync();

        if (requestCount > 50)
        {
            _logger.LogWarning("request rate limit.");
            return false;
        }

        await _asyncLock.WaitAsync();
        try
        {
            await _distributedCache.SetAsync(ZealyRateLimitKey, (requestCount++).ToString(),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = null,
                    AbsoluteExpirationRelativeToNow = null,
                    SlidingExpiration = null
                });
        }
        finally
        {
            _asyncLock.Release();
        }

        return true;
    }

    private async Task<int> GetRequestCountAsync()
    {
        var count = await _distributedCache.GetOrAddAsync(ZealyRateLimitKey, () => Task.FromResult("1"), () =>
            new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.Now.AddSeconds(1)
            });

        int.TryParse(count, out var requestCount);

        return requestCount;
    }
}