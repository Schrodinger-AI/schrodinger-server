using System.Collections.Generic;

namespace SchrodingerServer.EntityEventHandler.Core.Options;

public class RateLimitOption
{
    public string Name { get; set; }
    public int TokenLimit { get; set; }
    public int TokensPerPeriod { get; set; }
    public int ReplenishmentPeriod { get; set; }
}

public class RateLimitOptions
{
    public List<RateLimitOption> RedisRateLimitOptions { get; set; }
}