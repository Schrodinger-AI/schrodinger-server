using SchrodingerServer.Users.Index;

namespace SchrodingerServer.Tasks.Dtos;

public class ChangeTaskStatusInput
{
    public string Address { get; set; }
    public string TaskId { get; set; }
    public UserTaskStatus Status { get; set; }
    public string Date { get; set; }
}