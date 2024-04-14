using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Grains.Grain.Users;
using SchrodingerServer.PointServer;
using SchrodingerServer.Users.Dto;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.Users;

namespace SchrodingerServer.Users;

public class UserActionProvider : ApplicationService, IUserActionProvider
{
    private readonly ILogger<UserActionProvider> _logger;
    private readonly IClusterClient _clusterClient;
    private readonly IPointServerProvider _pointServerProvider;
    private readonly IDistributedCache<string> _checkDomainCache;
    private readonly IOptionsMonitor<AccessVerifyOptions> _accessVerifyOptions;
    private readonly IUserInformationProvider _userInformationProvider;

    public UserActionProvider(IClusterClient clusterClient, IPointServerProvider pointServerProvider,
        ILogger<UserActionProvider> logger, IDistributedCache<string> checkDomainCache,
        IOptionsMonitor<AccessVerifyOptions> accessVerifyOptions, IUserInformationProvider userInformationProvider)
    {
        _clusterClient = clusterClient;
        _pointServerProvider = pointServerProvider;
        _logger = logger;
        _checkDomainCache = checkDomainCache;
        _accessVerifyOptions = accessVerifyOptions;
        _userInformationProvider = userInformationProvider;
    }


    public async Task<bool> CheckDomainAsync(string domain)
    {
        // 1. The domain name must not be empty and matched pattern
        // 2. The domain name exists on the whitelist or on the points platform.
        return domain.NotNullOrEmpty() &&
               (_accessVerifyOptions.CurrentValue.HostWhiteList.Any(pattern => domain.Match(pattern)) ||
                domain.Match(@"^[a-zA-Z0-9-]{1,63}(\.[a-zA-Z0-9-]{1,63})*\.[a-zA-Z]{2,}(:[0-9]{2,5}){0,1}$") &&
                await CheckPointsDomainWithCacheAsync(domain));
    }

    private async Task<bool> CheckPointsDomainWithCacheAsync(string domain)
    {
        try
        {
            var cacheKey = "DomainCheck:" + domain;
            var cachedData = await _checkDomainCache.GetAsync(cacheKey);
            if (cachedData.NotNullOrEmpty())
                return bool.TryParse(cachedData, out var cachedValue) && cachedValue;

            var pointsServerCheck = await _pointServerProvider.CheckDomainAsync(domain);
            if (!pointsServerCheck) return false;

            // Only existing domain data is stored in the cache
            await _checkDomainCache.SetAsync(cacheKey, true.ToString(), new DistributedCacheEntryOptions
            {
                AbsoluteExpiration =
                    DateTimeOffset.Now.AddSeconds(_accessVerifyOptions.CurrentValue.DomainCacheSeconds)
            });
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Check domain error");
            return false;
        }
    }

    public async Task<DateTime?> GetActionTimeAsync(ActionType actionType)
    {
        if (CurrentUser is not { IsAuthenticated: true }) return null;
        var userId = CurrentUser.Id ?? Guid.Empty;
        if (userId == Guid.Empty) return null;

        var userActionGrain = _clusterClient.GetGrain<IUserActionGrain>(userId);
        return await userActionGrain.GetActionTime(actionType);
    }

    public async Task<UserActionGrainDto> AddActionAsync(ActionType actionType)
    {
        if (CurrentUser is not { IsAuthenticated: true }) return null;
        var userId = CurrentUser.Id ?? Guid.Empty;
        if (userId == Guid.Empty) return null;

        var userActionGrain = _clusterClient.GetGrain<IUserActionGrain>(userId);
        var res = await userActionGrain.AddActionAsync(actionType);
        AssertHelper.IsTrue(res?.Success ?? false, "Query action time failed");
        return res!.Data;
    }

    public async Task<MyPointDetailsDto> GetMyPointsAsync(GetMyPointsInput input)
    {
        var info = await _userInformationProvider.GetUserById(CurrentUser.GetId());
        if (info == null || String.IsNullOrEmpty(info.RegisterDomain))
        {
            return new MyPointDetailsDto();
        }

        input.Domain = info.RegisterDomain;
        _logger.Info("GetMyPoints by {0} {1}", input.Address, input.Domain);
        return await _pointServerProvider.GetMyPointsAsync(input);
    }
    public async Task<string> GetCurrentUserAddressAsync(string chainId)
    {
        var userId  = CurrentUser.IsAuthenticated ? CurrentUser.GetId() : Guid.Empty;
        string userAddress = null;
        if (userId != Guid.Empty)
        {
            var userGrain =  await _userInformationProvider.GetUserById(userId);

            userAddress = userGrain.AelfAddress;
        }
        _logger.LogInformation("Get current user address chainId: {chainId} address:{address}", chainId, userAddress);
        return userAddress;
    }
}