namespace SchrodingerServer.Tasks.Dtos;

public class SpinDto
{
    public string Seed { get; set; }
    public string Signature { get; set; }
    public long ExpirationTime { get; set; }
}