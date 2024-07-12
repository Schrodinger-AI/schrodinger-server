namespace SchrodingerServer.Dtos.Cat;

public class GetSchrodingerSoldInput
{
    public string FilterSymbol { get; set; }
    public long TimestampMin { get; set; }
    public long TimestampMax { get; set; }
    
    public string ChainId { get; set; }
}