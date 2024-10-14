namespace SchrodingerServer.Common.Options;

public class TasksOptions
{
    public List<TaskConfig> TaskList { get; set; }
}

public class TaskConfig
{
    public string TaskId { get; set; }
    public string Name { get; set; }
    public decimal Score { get; set; }
    public TaskType Type { get; set; }
    public string Link { get; set; }
}

public enum TaskType
{
    Daily = 1,
    Social = 2,
    Accomplishment = 3
}