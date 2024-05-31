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
}