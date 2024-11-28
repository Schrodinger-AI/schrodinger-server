using System.Collections.Generic;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Users.Index;

namespace SchrodingerServer.Tasks.Dtos;

public class GetTaskListOutput
{
    public List<TaskData> DailyTasks { get; set; } = new();
    public List<TaskData> SocialTasks { get; set; } = new();
    public  List<TaskData> AccomplishmentTasks { get; set; } = new();
    public  List<TaskData> PartnerTasks { get; set; } = new();
    public int Countdown { get; set; }
}

public class TaskData
{
    public string TaskId { get; set; }
    public string Name { get; set; }
    public UserTaskStatus Status { get; set; }
    public decimal Score { get; set; }
    public string Link { get; set; }
    public string LinkType { get; set; }
    public RewardType RewardType { get; set; }
    public TaskType Type { get; set; }
}