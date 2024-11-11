namespace SchrodingerServer.Dto;

public class PoolDataDto
{
    public string PoolId { get; set; }
    public string WinnerAddress { get; set; }
    public string WinnerSymbol { get; set; }
    public long Balance { get; set; }
}