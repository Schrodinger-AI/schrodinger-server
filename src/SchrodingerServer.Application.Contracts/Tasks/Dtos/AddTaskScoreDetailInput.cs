namespace SchrodingerServer.Tasks.Dtos;

public class AddTaskScoreDetailInput
{
    public string Address { get; set; }
    public string TaskId { get; set; }
    public decimal Score { get; set; }
    public string Id { get; set; }
}