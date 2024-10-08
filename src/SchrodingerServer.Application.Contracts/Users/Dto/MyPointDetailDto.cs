using System.Collections.Generic;

namespace SchrodingerServer.Users.Dto;

public class MyPointDetailsDto
{
    public bool HasBoundAddress { get; set; }
    public string EvmAddress { get; set; }
    public List<EarnedPointDto> PointDetails { get; set; } = new();
    public string TotalScore { get; set; }
    public string TotalReward{ get; set; }
}

public class ActionPoints
{
    public string Action { get; set; }
    public string Symbol { get; set; }
    public int Decimal { get; set; }
    public decimal Amount { get; set; }
}

public class EarnedPointDto : ActionPoints
{
    public decimal Rate { get; set; }
    public decimal InviteRate { get; set; }
    public long InviteFollowersNumber { get; set; }
    public decimal ThirdRate { get; set; }
    public long ThirdFollowersNumber { get; set; }
    public long UpdateTime { get; set; }
    public string DisplayName { get; set; }
    public decimal EcoEarnReward { get; set; }
}

public class EcoEarnRewardDto
{
    public Dictionary<string, string> Reward { get; set; }  // <PointName, Reward>
}

public class GetEcoEarnRewardRequest
{
    public string Address { get; set; }
    public string DappId { get; set; }
}

public class EcoEarnTotalRewardDto
{
    public string TotalReward { get; set; }
}
