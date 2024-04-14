using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Options;
using RedisRateLimiting;
using SchrodingerServer.EntityEventHandler.Core.Options;
using StackExchange.Redis;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.EntityEventHandler.Core.IndexHandler;

public interface IRateDistributeLimiter
{
    RedisTokenBucketRateLimiter<string> GetRateLimiterInstance(string resourceName);
}

public class RateDistributeLimiter : IRateDistributeLimiter, ISingletonDependency
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly RateLimitOptions _rateLimitOptions;
    private readonly ConcurrentDictionary<string, RedisTokenBucketRateLimiter<string>> _rateDistributeLimiters = new();

    public RateDistributeLimiter(IConnectionMultiplexer connectionMultiplexer, IOptionsMonitor<RateLimitOptions> rateLimitOptions)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _rateLimitOptions = rateLimitOptions.CurrentValue;
    }

    public RedisTokenBucketRateLimiter<string> GetRateLimiterInstance(string resourceName)
    {
        return _rateDistributeLimiters.GetOrAdd(resourceName, key =>
        {
            var option = _rateLimitOptions.RedisRateLimitOptions.FirstOrDefault(x => x.Name.Equals(resourceName)) ?? new RateLimitOption()
            {
                TokenLimit = 100,
                TokensPerPeriod = 100,
                ReplenishmentPeriod = 1,
            };

            return new RedisTokenBucketRateLimiter<string>(key, new RedisTokenBucketRateLimiterOptions
            {
                TokenLimit = option.TokenLimit,
                TokensPerPeriod = option.TokensPerPeriod,
                ReplenishmentPeriod = TimeSpan.FromSeconds(option.ReplenishmentPeriod),
                ConnectionMultiplexerFactory = () => _connectionMultiplexer
            });
        });
    }
}