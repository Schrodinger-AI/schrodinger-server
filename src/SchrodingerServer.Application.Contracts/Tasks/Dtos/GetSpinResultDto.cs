namespace SchrodingerServer.Tasks.Dtos;


public class SpinResultDto
{
    public string Address { get; set; }
    public string SpinId { get; set; }
    public string Name { get; set; }
}

public class GetSpinResultQueryDto
{
    public SpinResultDto GetSpinResult { get; set; }
}