using System;
using System.Collections.Generic;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Users.Index;

namespace SchrodingerServer.Tasks.Dtos;

public class TasksDto
{
    public string Id { get; set; }
    public string TaskId { get; set; }
    public string Address { get; set; }
    public string Name { get; set; }
    public UserTaskStatus Status { get; set; }
    public decimal Score { get; set; }
    public string Date { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    public string Link { get; set; }
    public string LinkType { get; set; }
    public RewardType RewardType { get; set; }
    public TaskType Type { get; set; }
}

public class TaskListDto
{
    public List<TasksDto> TaskList { get; set; } = new();
}

public class MilestoneTaskCache
{
    public string TaskId { get; set; }
    public int Level { get; set; }
}