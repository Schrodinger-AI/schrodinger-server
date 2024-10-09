using System.Collections.Generic;
using SchrodingerServer.Users.Index;

namespace SchrodingerServer.Tasks.Dtos;

public class GetTaskListOutput
{
    public List<TaskData> DailyTasks { get; set; } = new();
    public List<TaskData> SocialTasks { get; set; } = new();
    public  List<TaskData> AccomplishmentTasks { get; set; } = new();
    
    public decimal FishScore { get; set; }
}

public class TaskData
{
    public string TaskId { get; set; }
    public string Name { get; set; }
    public UserTaskStatus Status { get; set; }
}