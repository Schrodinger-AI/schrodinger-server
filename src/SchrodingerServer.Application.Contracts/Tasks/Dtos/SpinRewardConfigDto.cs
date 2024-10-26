using System.Collections.Generic;

namespace SchrodingerServer.Tasks.Dtos;

public class SpinRewardConfigDto
{
    public List<RewardDto> RewardList { get; set; }
}


public class SpinRewardConfigQueryDto
{
    public SpinRewardConfigDto GetLatestSpinRewardConfig { get; set; }
}

public class RewardDto
{
    public string Name { get; set; }
    public long Amount { get; set; }
    public string Content { get; set; }
}