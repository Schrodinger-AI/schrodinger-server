using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.ExceptionHandling;
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
    private readonly IAddressRelationshipProvider _addressRelationshipProvider;

    private readonly Dictionary<string, string> _domainDict = new Dictionary<string, string>
    {
        { "sgr.schrodingerai.com", "schrodingerai.com" },
        { "sgr.schrodingernft.ai", "schrodingernft.ai" }
    };
    

    public UserActionProvider(IClusterClient clusterClient, IPointServerProvider pointServerProvider,
        ILogger<UserActionProvider> logger, IDistributedCache<string> checkDomainCache,
        IOptionsMonitor<AccessVerifyOptions> accessVerifyOptions, IUserInformationProvider userInformationProvider, 
        IAddressRelationshipProvider addressRelationshipProvider)
    {
        _clusterClient = clusterClient;
        _pointServerProvider = pointServerProvider;
        _logger = logger;
        _checkDomainCache = checkDomainCache;
        _accessVerifyOptions = accessVerifyOptions;
        _userInformationProvider = userInformationProvider;
        _addressRelationshipProvider = addressRelationshipProvider;
    }


    public async Task<bool> CheckDomainAsync(string domain)
    {
        _logger.LogDebug("CheckDomain :{domain}", domain);
        if (_domainDict.TryGetValue(domain, out var value))
        {
            domain = value;
        }
        
        // 1. The domain name must not be empty and matched pattern
        // 2. The domain name exists on the whitelist or on the points platform.
        return domain.NotNullOrEmpty() &&
               (_accessVerifyOptions.CurrentValue.HostWhiteList.Any(pattern => domain.Match(pattern)) ||
                domain.Match(@"^[a-zA-Z0-9-]{1,63}(\.[a-zA-Z0-9-]{1,63})*\.[a-zA-Z]{2,}(:[0-9]{2,5}){0,1}$") &&
                await CheckPointsDomainWithCacheAsync(domain));
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionFalse))]
    private async Task<bool> CheckPointsDomainWithCacheAsync(string domain)
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
        _logger.LogDebug("GetMyPoints by {0} {1}", input.Address, input.Domain);
        
        var res = await _pointServerProvider.GetMyPointsAsync(input);

        var ecoEarnRewards = await _pointServerProvider.GetEcoEarnRewardsAsync(input.Address);
        res.PointDetails.ForEach(detail =>
        {
            if (ecoEarnRewards.Reward.TryGetValue(detail.Symbol, out var value))
            {
                detail.EcoEarnReward = decimal.Parse(value);
            }
            else
            {
                detail.EcoEarnReward = 0;   
            }
        });
        
        res.PointDetails = res.PointDetails.OrderByDescending(o =>
        {
            var symbol = o.Symbol;
            var symbolData = o.Symbol.Split("-");
            if (symbolData.Length != 2)
            {
                return 0;
            }

            return int.Parse(symbolData[1]);
        }).ToList();
        
        var hasBoundAddress = await _addressRelationshipProvider.CheckBindingExistsAsync(info.AelfAddress, "");
        res.HasBoundAddress = hasBoundAddress;
        var evmAddress = await  _addressRelationshipProvider.GetEvmAddressByAelfAddressAsync(info.AelfAddress);
        if (!evmAddress.IsNullOrEmpty())
        {
            res.EvmAddress = evmAddress;
            res.HasBoundAddress = true;
        }

        decimal totalAmount = 0;
        foreach (var pointData in res.PointDetails)
        {
            var amount = pointData.Amount;
            totalAmount += amount;
        }

        res.TotalScore = totalAmount.ToString();
        
        var totalRewardDto = await _pointServerProvider.GetEcoEarnTotalRewardsAsync(input.Address);
        if (totalRewardDto.TotalReward.NotNullOrEmpty())
        {
            res.TotalReward = totalRewardDto.TotalReward;
        }
        
        return res;
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
        _logger.LogInformation("Get current user address:{address}", userAddress);
        return userAddress;
    }
    
    public async Task<string> GetCurrentUserAddressAsync()
    {
        var userId  = CurrentUser.IsAuthenticated ? CurrentUser.GetId() : Guid.Empty;
        string userAddress = null;
        if (userId != Guid.Empty)
        {
            var userGrain =  await _userInformationProvider.GetUserById(userId);
            userAddress = userGrain == null ? "" : (userGrain.AelfAddress.IsNullOrEmpty()?userGrain.CaAddressMain:userGrain.AelfAddress);
        }

        if (userAddress.IsNullOrEmpty())
        {
            _logger.LogInformation("current user address empty");
        }
        else
        {
            _logger.LogInformation("Get current user address address:{address}", userAddress);
        }
        
        return userAddress;
    }
}