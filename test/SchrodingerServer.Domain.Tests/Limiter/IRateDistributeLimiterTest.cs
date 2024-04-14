using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Nest;
using RedisRateLimiting;
using SchrodingerServer.EntityEventHandler.Core.IndexHandler;
using SchrodingerServer.EntityEventHandler.Core.Options;
using Shouldly;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace SchrodingerServer.Limiter;

public class IRateDistributeLimiterTest : SchrodingerServerDomainTestBase
{
    private IRateDistributeLimiter _rateDistributeLimiter { get; set; }
    private ITestOutputHelper Output { get; }

    public IRateDistributeLimiterTest(ITestOutputHelper output) : base(output)
    {
        Output = output;
        var monitor = Mock.Of<IOptionsMonitor<RateLimitOptions>>(x => x.CurrentValue == new RateLimitOptions()
        {
            RedisRateLimitOptions = new List<RateLimitOption>()
            {
                new()
                {
                    Name = "test",
                    TokenLimit = 2,
                    TokensPerPeriod = 2,
                    ReplenishmentPeriod = 60
                }
            }
        });
        _rateDistributeLimiter = new RateDistributeLimiter(GetService<IConnectionMultiplexer>(), monitor);
    }

    [Fact]
    public async void Test2()
    {
        var limiter = _rateDistributeLimiter.GetRateLimiterInstance("test");
        for (int i = 0; i < 1000; i++)
        {
            var lease = await limiter.AcquireAsync();
            if (lease.IsAcquired)
            {
                Output.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} " + i + " success ");
            }
            else
            {
                lease.TryGetMetadata(RateLimitMetadataName.RetryAfter.Name, out var retryAfter);
                Output.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} " + i + " failed " + (int)retryAfter);
                await Task.Delay((int)retryAfter * 1000);
            }
        }
    }

    [Fact]
    public async void Test1()
    {
        var limiter = _rateDistributeLimiter.GetRateLimiterInstance("test");
        limiter.ShouldNotBeNull();
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await limiter.AcquireAsync(2));
        Assert.Equal("permitCount", ex.ParamName);
    }
}