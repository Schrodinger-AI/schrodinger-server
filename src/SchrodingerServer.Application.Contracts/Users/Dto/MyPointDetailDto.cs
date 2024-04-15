using System.Collections.Generic;

namespace SchrodingerServer.Users.Dto;

public class MyPointDetailsDto
{
    public List<EarnedPointDto> PointDetails { get; set; } = new();
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
}