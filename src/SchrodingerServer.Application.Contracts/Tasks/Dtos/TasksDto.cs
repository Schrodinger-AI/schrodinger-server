using System;
using System.Collections.Generic;
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
}

public class TaskListDto
{
    public List<TasksDto> TaskList { get; set; } = new();
}