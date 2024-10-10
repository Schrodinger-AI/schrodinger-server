using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SchrodingerServer.Background.Dtos;
using SchrodingerServer.Background.Providers;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.ExceptionHandling;
using SchrodingerServer.Zealy;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Background.Services;

public interface IUserRelationService
{
    Task AddUserRelationAsync();
}

public class UserRelationService : IUserRelationService, ISingletonDependency
{
    //private readonly IZealyClientProxyProvider _zealyClientProxyProvider;
    private readonly IZealyClientProvider _zealyClientProxyProvider;
    private readonly ILogger<UserRelationService> _logger;
    private readonly INESTRepository<ZealyUserIndex, string> _zealyUserRepository;
    private readonly IDistributedCache<ReviewsCursorInfo> _distributedCache;
    private readonly IZealyProvider _zealyProvider;
    private readonly ZealyUserOptions _options;
    private int _retryCount = 0;

    public UserRelationService(IZealyClientProvider zealyClientProxyProvider,
        ILogger<UserRelationService> logger,
        INESTRepository<ZealyUserIndex, string> zealyUserRepository,
        IDistributedCache<ReviewsCursorInfo> distributedCache,
        IOptionsSnapshot<ZealyUserOptions> options,
        IZealyProvider zealyProvider)
    {
        _logger = logger;
        _zealyClientProxyProvider = zealyClientProxyProvider;
        _zealyUserRepository = zealyUserRepository;
        _distributedCache = distributedCache;
        _zealyProvider = zealyProvider;
        _options = options.Value;
    }

    public async Task AddUserRelationAsync()
    {
        _logger.LogInformation("AddUserRelationAsync begin to execute.");
        await AddZealyUserWithRetryFromBeginAsync();
        await AddZealyUserWithRetryAsync();
        _logger.LogInformation("AddUserRelationAsync end to execute.");
    }

    private async Task AddZealyUserWithRetryFromBeginAsync()
    {
        var nextCursor = string.Empty;
        var cursorInfo = await _distributedCache.GetAsync(nameof(ReviewsCursorInfo));

        if (cursorInfo == null)
        {
            return;
        }

        _logger.LogInformation("add zealy user from begin, nextCursor:{nextCursor}", nextCursor);
        await AddZealyUserFromBeginAsync(nextCursor);
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    private async Task AddZealyUserFromBeginAsync(string nextCursor)
    {
        var uri = CommonConstant.GetReviewsUri + $"?questId={_options.QuestId}&limit={_options.Limit}";
        if (!nextCursor.IsNullOrEmpty())
        {
            uri += $"&cursor={nextCursor}";
        }

        _logger.LogInformation("get user from zealy, uri:{uri}", uri);
        var response = await _zealyClientProxyProvider.GetAsync<ReviewDto>(uri);

        if (response.NextCursor == null)
        {
            _logger.LogInformation("add zealy user from begin finish");
            return;
        }

        // mapping index
        var users = GetUserIndices(response.Items);
        var user = response.Items.Last();

        var zealyUser = await _zealyProvider.GetUserByIdAsync(user.User.Id);
        await _zealyUserRepository.BulkAddOrUpdateAsync(users);

        if (zealyUser != null)
        {
            _logger.LogInformation("last item already save, add zealy user from begin finish, lastUserId:{userId}",
                zealyUser.Id);
            return;
        }

        await AddZealyUserFromBeginAsync(response.NextCursor);
    }

    private async Task AddZealyUserWithRetryAsync()
    {
        var nextCursor = string.Empty;
        var cursorInfo = await _distributedCache.GetAsync(nameof(ReviewsCursorInfo));

        if (cursorInfo != null)
        {
            nextCursor = cursorInfo.NextCursor;
        }

        await AddZealyUserAsync(nextCursor);
    }
    
    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    private async Task AddZealyUserAsync(string nextCursor)
    {
        var uri = CommonConstant.GetReviewsUri + $"?questId={_options.QuestId}&limit={_options.Limit}";
        if (!nextCursor.IsNullOrEmpty())
        {
            uri += $"&cursor={nextCursor}";
        }

        _logger.LogInformation("get user from zealy, uri:{uri}", uri);
        var response = await _zealyClientProxyProvider.GetAsync<ReviewDto>(uri);
        if (response.NextCursor == null)
        {
            _logger.LogInformation("add zealy user finish");
            return;
        }

        // mapping index
        var users = GetUserIndices(response.Items);
        await _zealyUserRepository.BulkAddOrUpdateAsync(users);

        await SetCursorInfoAsync(new ReviewsCursorInfo
        {
            NextCursor = response.NextCursor,
            UpdateTime = DateTime.UtcNow
        });

        await AddZealyUserAsync(response.NextCursor);
    }

    private List<ZealyUserIndex> GetUserIndices(List<ReviewItem> reviewItems)
    {
        var users = new List<ZealyUserIndex>();
        if (reviewItems.IsNullOrEmpty())
        {
            return users;
        }

        foreach (var item in reviewItems)
        {
            if (item.Tasks.Count == 0)
            {
                _logger.LogError("user share wallet address task empty, data:{data}",
                    JsonConvert.SerializeObject(item));
                continue;
            }

            if (item.Tasks.Count > 1)
            {
                _logger.LogError("user share wallet address task count more than 1, data:{data}",
                    JsonConvert.SerializeObject(item));
                continue;
            }

            var shareTask = item.Tasks.First();

            var address = GetAddress(shareTask.Value);

            var user = new ZealyUserIndex
            {
                Id = item.User.Id,
                Address = address,
                CreateTime = item.Tasks.First().CreatedAt,
                UpdateTime = DateTime.UtcNow
            };

            users.Add(user);
        }

        return users;
    }

    private string GetAddress(string value)
    {
        if (value.IsNullOrEmpty() || !value.Trim().StartsWith("ELF_"))
        {
            throw new Exception($"invalid value address {value}");
        }

        var str = value.Trim().Split('_');

        // need check _tDVV ?
        return str[1];
    }

    private async Task SetCursorInfoAsync(ReviewsCursorInfo cursorInfo)
    {
        await _distributedCache.SetAsync(nameof(ReviewsCursorInfo), cursorInfo, new DistributedCacheEntryOptions()
        {
            AbsoluteExpiration = CommonConstant.DefaultAbsoluteExpiration
        });
    }
}