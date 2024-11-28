namespace SchrodingerServer.Common.Options;

public class TasksOptions
{
    public List<TaskConfig> TaskList { get; set; }
    public int InviteLimit { get; set; } = 100;
    public List<string> Whitelist { get; set; }
}

public class TaskConfig
{
    public string TaskId { get; set; }
    public string Name { get; set; }
    public decimal Score { get; set; }
    public TaskType Type { get; set; }
    public string Link { get; set; }
    public string LinkType { get; set; }
    public string Milestone { get; set; }
    
    public RewardType RewardType { get; set; }
}

public enum TaskType
{
    Daily = 1,
    Social = 2,
    Accomplishment = 3,
    Partner = 4
}

public enum RewardType
{
    FishScore,
    Voucher
}