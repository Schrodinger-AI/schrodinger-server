using SchrodingerServer.Users.Index;

namespace SchrodingerServer.Tasks.Dtos;

public class ClaimOutput
{
    public string TaskId { get; set; }
    public string Name { get; set; }
    public UserTaskStatus Status { get; set; }
    public decimal FishScore { get; set; } 
}
