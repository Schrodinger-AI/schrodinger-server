namespace SchrodingerServer.Tasks.Dtos;

public class AddSpinInput
{
    public string Seed { get; set; }
    public string Address { get; set; }
    public string Tick { get; set; }
    public string Signature { get; set; }
    public long ExpirationTime { get; set; }
}