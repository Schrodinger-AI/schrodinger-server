namespace SchrodingerServer.Common.Options;

public class SpinRewardOptions
{
    public List<RewardItem> RewardList  { get; set; }
}

public class RewardItem
{
    public string Name { get; set; }
    public string Content { get; set; }
}