namespace SchrodingerServer.Common.Options;

public class ZealyUserOptions
{
    public string QuestId { get; set; }
    public int Limit { get; set; } = 10;
    
    //minute
    public int Period { get; set; } = 60;
    public int RetryTime { get; set; } = 5;
    public int RetryDelayTime { get; set; } = 3;
}