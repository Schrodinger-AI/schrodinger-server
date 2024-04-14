using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Nest;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.EntityEventHandler.Core.IndexHandler;

public interface IRequestLimitProvider
{
    Task RecordRequestAsync(string resource, int delayTime = 1000, int maxRequestTime = 100);
}

public class RequestLimitProvider : IRequestLimitProvider, ISingletonDependency
{
    private readonly IDistributedCache<RequestTime> _requestTimeCache;


    public RequestLimitProvider(IDistributedCache<RequestTime> requestTimeCache)
    {
        _requestTimeCache = requestTimeCache;
    }

    public async Task RecordRequestAsync(string resource, int delayTime = 10, int maxRequestTime = 100)
    {
        var requestTime = await _requestTimeCache.GetOrAddAsync(resource,
            async () => new RequestTime(), () => new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(1)
            });
        requestTime.Time += 1;

        if (requestTime.Time > maxRequestTime)
        {
            //default delay time is 10ms, so 100 requests/minute
            await Task.Delay(delayTime);
        }

        await _requestTimeCache.SetAsync(resource, requestTime);
    }
}

public class RequestTime
{
    public int Time { get; set; }
}