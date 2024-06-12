using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SchrodingerServer.Activity.Eto;
using SchrodingerServer.Adopts.provider;
using SchrodingerServer.Common;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace SchrodingerServer.EntityEventHandler.Core.IndexHandler;

public class UpdateAddressCacheHandler : IDistributedEventHandler<UpdateAddressCacheEto>, ITransientDependency
{
    private readonly ILogger<UpdateAddressCacheHandler> _logger;
    private readonly IDistributedCache<string> _distributedCache;
    private readonly IAdoptGraphQLProvider _adoptGraphQlProvider;
    private readonly IPortkeyProvider _portkeyProvider;

    public UpdateAddressCacheHandler(
        IDistributedCache<string> distributedCache,
        ILogger<UpdateAddressCacheHandler> logger, 
        IAdoptGraphQLProvider adoptGraphQlProvider, 
        IPortkeyProvider portkeyProvider)
    {
        _distributedCache = distributedCache;
        _logger = logger;
        _adoptGraphQlProvider = adoptGraphQlProvider;
        _portkeyProvider = portkeyProvider;
    }

    public async Task HandleEventAsync(UpdateAddressCacheEto eventData)
    {
        _logger.LogInformation("UpdateAddressCache, req:{req}", JsonConvert.SerializeObject(eventData));
        var addressList = await _adoptGraphQlProvider.GetAdoptAddressByTime(eventData.BeginTime, eventData.EndTime);
        if (addressList.IsNullOrEmpty())
        {
            _logger.LogInformation("UpdateAddressCache No Data");
            return;
        }

        var uniqueAddress = new HashSet<string>(addressList);
        var res = await _portkeyProvider.BatchGetAddressInfo(uniqueAddress.ToList());
        var portkeyAddressList = res.Select(x => x.CaAddress).ToList();
        
        foreach (var address in uniqueAddress)
        {
            if (address.IsNullOrEmpty())
            {
                _logger.LogError("address is null}");
                continue;
            }
            
            var isEoa = !portkeyAddressList.Contains(address);
            _logger.LogInformation("{address} is EOA Address: {isEoa}", address, isEoa);
            var id = IdGenerateHelper.GetEOAAddressCacheKey(address);
            await _distributedCache.SetAsync(id, isEoa.ToString(),  new DistributedCacheEntryOptions()
            {
                SlidingExpiration = TimeSpan.FromDays(300)
            });
        }
    }
}