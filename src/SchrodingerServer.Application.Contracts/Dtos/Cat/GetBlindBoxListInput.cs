namespace SchrodingerServer.Dtos.Cat;

public class GetBlindBoxListInput
{
    public string Address { get; set; }
    public long Height { get; set; }
    public int SkipCount { get; set; } 
    public int MaxResultCount { get; set; }
}