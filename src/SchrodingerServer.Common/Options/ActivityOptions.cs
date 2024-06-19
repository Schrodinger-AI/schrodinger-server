namespace SchrodingerServer.Common.Options;

public class ActivityOptions
{
    public List<ActivityInfo> ActivityList { get; set; }
    
    public long NewTagInterval { get; set; } = 72 * 60 * 60; 
}

public class ActivityInfo
{
    public string ActivityId { get; set; }
    public string ActivityName { get; set; }
    public string BannerUrl { get; set; }
    public string LinkUrl { get; set; }
    public string LinkType { get; set; }
    public long BeginTime { get; set; }
    public long EndTime { get; set; }
    public bool IsShow { get; set; }
    public bool IsNew { get; set; } = true;
    public string TimeDescription { get; set; }
}


public class ActivityRankOptions
{
    public  List<RankHeader> Header { get; set; }
    
    public  List<RankRewardItem> RankRewards { get; set; }
    
    public long BeginTime { get; set; }
    
    public long EndTime { get; set; }
    
    public long DisplayBeginTime { get; set; }
    
    public long DisplayEndTime { get; set; }
    
    public long NormalDisplayNumber { get; set; }
    
    public  long FinalDisplayNumber { get; set; }
}

public class RankHeader
{
    public string Key { get; set; }
    public string Type { get; set; }
    public string Title { get; set; }
    public long Width { get; set; }
    public List<string> Tooltip { get; set; }
}

public class RankRewardItem
{
    public int Rank { get; set; }
   
    public long Reward { get; set; }
}
