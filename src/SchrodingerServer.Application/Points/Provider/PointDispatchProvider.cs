using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Orleans;
using SchrodingerServer.Grains.Grain.Points;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Points.Provider;

public interface IPointDispatchProvider
{
    Task<bool> GetDispatchAsync(string prefix, string bizDate, string pointName);
    Task SetDispatchAsync(string prefix, string bizDate, string pointName, bool isDispatched);
    Task<int> GetDailyChangeHeightAsync(string prefix, string bizDate, string pointName);
    Task SetDailyChangeHeightAsync(string prefix, string bizDate, string pointName, int height);
    
}

public class PointDispatchProvider : IPointDispatchProvider,ISingletonDependency
{
    private readonly IClusterClient _clusterClient;
    private readonly IDistributedCache<string> _distributedCache;
    
    public PointDispatchProvider(IClusterClient clusterClient,IDistributedCache<string> distributedCache)
    {
        _clusterClient = clusterClient;
        _distributedCache = distributedCache;
    }


    public async Task<bool> GetDispatchAsync(string prefix, string bizDate, string pointName)
    {
        var id = GetId(prefix, bizDate, pointName);
        var isDispatched = await _distributedCache.GetAsync(id);
        if (isDispatched != null)
        {
            return bool.Parse(isDispatched);
        }
        return  false;
        // var re= await _clusterClient.GetGrain<IPointDailyDispatchGrain>(id).GetPointDailyDispatchGrainAsync();
        // return re.Data.Executed;
    }

    public async Task SetDispatchAsync(string prefix, string bizDate, string pointName, bool isDispatched)
    {
        var id = GetId(prefix, bizDate, pointName);
        await _distributedCache.SetAsync(id, isDispatched.ToString(),  new DistributedCacheEntryOptions()
        {
            SlidingExpiration = TimeSpan.FromDays(7)
        });
        // await _clusterClient.GetGrain<IPointDailyDispatchGrain>(id).SavePointDailyDispatch(new PointDailyDispatchGrainDto
        // {
        //     Id = id,
        //     BizDate = bizDate,
        //     CreateTime = DateTime.UtcNow,
        //     Executed = isDispatched
        // });
        
    }

    public async Task<int> GetDailyChangeHeightAsync(string prefix, string bizDate, string pointName)
    {
        var id = GetId(prefix, bizDate, pointName);
        var isDispatched = await _distributedCache.GetAsync(id);
        if (isDispatched != null)
        {
            return int.Parse(isDispatched);
        }
        var re= await _clusterClient.GetGrain<IPointDailyDispatchGrain>(id).GetPointDailyDispatchGrainAsync();
        return re.Data.Height;
    }

    public async Task SetDailyChangeHeightAsync(string prefix,string bizDate, string pointName, int height)
    {
        var id = GetId(prefix, bizDate, pointName);
        await _distributedCache.SetAsync(id, height.ToString(),  new DistributedCacheEntryOptions()
        {
            SlidingExpiration = TimeSpan.FromDays(7)
        });
        await _clusterClient.GetGrain<IPointDailyDispatchGrain>(id).SavePointDailyDispatch(new PointDailyDispatchGrainDto
        {
            Id = id,
            BizDate = bizDate,
            CreateTime = DateTime.UtcNow,
            Executed = true,
            Height = height
        });
    }

    private string GetId(string prefix, string bizDate, string pointName)
    {
        return $"{prefix}-{bizDate}-{pointName}";
    }

}