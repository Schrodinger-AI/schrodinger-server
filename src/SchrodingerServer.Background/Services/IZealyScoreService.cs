using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.ExceptionHandler;
using Hangfire;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using SchrodingerServer.Background.Dtos;
using SchrodingerServer.Background.Providers;
using SchrodingerServer.Common;
using SchrodingerServer.Grains.Grain.ZealyScore;
using SchrodingerServer.Grains.Grain.ZealyScore.Dtos;
using SchrodingerServer.Common.Options;
using SchrodingerServer.ExceptionHandling;
using SchrodingerServer.Options;
using SchrodingerServer.Zealy;
using Volo.Abp;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Background.Services;

public interface IZealyScoreService
{
    Task UpdateScoreAsync();
}

public class ZealyScoreService : IZealyScoreService, ISingletonDependency
{
    private readonly ILogger<ZealyScoreService> _logger;
    private readonly IUserRelationService _userRelationService;
    private readonly IZealyProvider _zealyProvider;

    //private readonly IZealyClientProxyProvider _zealyClientProxyProvider;
    private readonly IZealyClientProvider _zealyClientProxyProvider;
    private readonly IXpRecordProvider _xpRecordProvider;
    private readonly ZealyScoreOptions _options;
    private List<ZealyXpScoreIndex> _zealyXpScores = new();
    private readonly IDistributedCache<UpdateScoreInfo> _distributedCache;
    private readonly IClusterClient _clusterClient;
    private const string _updateScorePrefix = "UpdateZealyScoreInfo";

    public ZealyScoreService(ILogger<ZealyScoreService> logger,
        IUserRelationService userRelationService,
        IZealyProvider zealyProvider,
        IZealyClientProvider zealyClientProxyProvider,
        IXpRecordProvider xpRecordProvider,
        IOptionsSnapshot<ZealyScoreOptions> options,
        IDistributedCache<UpdateScoreInfo> distributedCache,
        IClusterClient clusterClient)
    {
        _logger = logger;
        _userRelationService = userRelationService;
        _zealyProvider = zealyProvider;
        _zealyClientProxyProvider = zealyClientProxyProvider;
        _xpRecordProvider = xpRecordProvider;
        _distributedCache = distributedCache;
        _clusterClient = clusterClient;
        _options = options.Value;
    }

    public async Task UpdateScoreAsync()
    {
        await CheckAndHandle(); 
        await _distributedCache.RemoveAsync(GetCacheKey());
        
    }
    
    [ExceptionHandler(typeof(Exception), Message = "update zealy score error", TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    private async Task CheckAndHandle()
    {
        var jobIsStart = await CheckJobAsync();
        if (!jobIsStart)
        {
            _logger.LogWarning("update zealy score recurring job is started");
            return;
        }

        _logger.LogInformation("begin update zealy score recurring job");
        // update user
        await _userRelationService.AddUserRelationAsync();

        // wait es synchronization
        await Task.Delay(1000);

        await HandleUserScoreAsync();
        _logger.LogInformation("finish update zealy score recurring job");
    }

    private async Task<bool> CheckJobAsync()
    {
        var key = GetCacheKey();
        var cache = await _distributedCache.GetAsync(key);
        if (cache != null)
        {
            return false;
        }

        await _distributedCache.SetAsync(key, new UpdateScoreInfo()
        {
            UpdateTime = DateTime.UtcNow
        }, new DistributedCacheEntryOptions()
        {
            AbsoluteExpiration = DateTimeOffset.Now.AddHours(6)
        });

        return true;
    }

    private string GetCacheKey()
    {
        return $"{_updateScorePrefix}:{DateTime.UtcNow:yyyy-MM-dd}";
    }

    private async Task GetUsersAsync(List<ZealyUserIndex> userIndices,
        int skipCount, int maxResultCount)
    {
        var users =
            await _zealyProvider.GetUsersAsync(skipCount, maxResultCount);
        userIndices.AddRange(users);

        if (users.IsNullOrEmpty() || users.Count < maxResultCount)
        {
            return;
        }

        skipCount += maxResultCount;
        await GetUsersAsync(userIndices, skipCount, maxResultCount);
    }

    private async Task GetXpScoresAsync(List<ZealyXpScoreIndex> xpScoreIndices,
        int skipCount, int maxResultCount)
    {
        var xpScores =
            await _zealyProvider.GetXpScoresAsync(skipCount, maxResultCount);
        xpScoreIndices.AddRange(xpScores);

        if (xpScores.IsNullOrEmpty() || xpScores.Count < maxResultCount)
        {
            return;
        }

        skipCount += maxResultCount;
        await GetXpScoresAsync(xpScoreIndices, skipCount, maxResultCount);
    }

    private async Task HandleUserScoreAsync()
    {
        var users = new List<ZealyUserIndex>();
        await GetUsersAsync(users, 0, _options.FetchCount);
        await GetXpScoresAsync(_zealyXpScores, 0, _options.FetchCount);

        foreach (var user in users)
        {
            await ProcessUser(user);
        }
    }
    
    [ExceptionHandler(typeof(Exception), Message = "handle user score error", TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    private async Task ProcessUser(ZealyUserIndex user)
    {
        if (!ValidAddress(user.Address))
        {
            return;
        }

        await HandleUserScoreAsync(user);
    }

    private async Task HandleUserScoreAsync(ZealyUserIndex user)
    {
        var userDto = await GetZealyUserAsync(user.Id);
        if (userDto == null)
        {
            return;
        }

        var xp = 0m;
        var userXpScore = _zealyXpScores.FirstOrDefault(t => t.Id == user.Id);

        var repairScore = 0m;
        if (userXpScore != null)
        {
            repairScore = userXpScore.ActualScore - userXpScore.RawScore;
        }

        var currentXp = userDto.Xp + repairScore;

        var userXp = await GetUserXpAsync(user.Id, user.Address);

        if (userXp == 0)
        {
            xp = currentXp;
            _logger.LogInformation(
                "calculate xp, userId:{userId}, responseXp:{responseXp}, userXp:{userXp},  xp:{xp}, currentXp:{currentXp}",
                user.Id, userDto.Xp, userXp, xp, currentXp);
        }
        else
        {
            xp = currentXp - userXp;
            _logger.LogInformation(
                "calculate xp, userId:{userId}, responseXp:{responseXp}, userXp:{userXp}, xp:{xp}, currentXp:{currentXp}",
                user.Id, userDto.Xp, userXp, xp, currentXp);
        }

        if (xp > 0)
        {
            BackgroundJob.Enqueue(() => _xpRecordProvider.CreateRecordAsync(user.Id, user.Address, currentXp, xp));
        }
    }

    private async Task<decimal> GetUserXpAsync(string userId, string address)
    {
        var userXpGrain = _clusterClient.GetGrain<IZealyUserXpGrain>(userId);
        var resultDto = await userXpGrain.GetUserXpInfoAsync();

        if (resultDto.Success)
        {
            return resultDto.Data.CurrentXp;
        }

        _logger.LogError(
            "get user xp info fail, message:{message}, userId:{userId}",
            resultDto.Message, userId);

        if (resultDto.Message == ZealyErrorMessage.UserXpInfoNotExistCode)
        {
            // add user if not exist.
            await AddUserXpInfoAsync(userId, address);
            return 0;
        }

        throw new UserFriendlyException("get user xp error, message:{message}", resultDto.Message);
    }

    private async Task<ZealyUserXpGrainDto> AddUserXpInfoAsync(string userId, string address)
    {
        var userXpGrain = _clusterClient.GetGrain<IZealyUserXpGrain>(userId);
        var userDto = new ZealyUserXpGrainDto()
        {
            Id = userId,
            Address = address
        };
        var result = await userXpGrain.AddUserXpInfoAsync(userDto);

        if (result.Success)
        {
            return result.Data;
        }

        _logger.LogError(
            "add user xp info fail, message:{message}, userId:{userId}, address:{address}",
            result.Message, userId, address);

        throw new UserFriendlyException(result.Message);
    }
    
    [ExceptionHandler(typeof(Exception), ReturnDefault = ReturnDefault.New, TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    private async Task<ZealyUserDto> GetZealyUserAsync(string userId)
    {
        var uri = CommonConstant.GetUserUri + $"/{userId}";
        _logger.LogInformation("get user info, uri:{uri}", uri);
        return await _zealyClientProxyProvider.GetAsync<ZealyUserDto>(uri);
    }
    
    [ExceptionHandler(typeof(Exception), ReturnDefault = ReturnDefault.Default, TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    private bool ValidAddress(string address)
    {
        var isValid = AddressHelper.VerifyFormattedAddress(address);
        if (!isValid)
        {
            _logger.LogInformation("invalid address: {address}", address);
        }

        return isValid;
    }
}