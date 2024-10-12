namespace SchrodingerServer.Tasks.Dtos;

public class FinishInput
{
    public string TaskId { get; set; }
    public string Address { get; set; }
}

public class ClaimInput
{
    public string TaskId { get; set; }
    public string Address { get; set; }
}