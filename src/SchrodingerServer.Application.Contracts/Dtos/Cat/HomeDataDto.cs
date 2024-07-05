namespace SchrodingerServer.Dtos.Cat;

public class HomeDataDto
{
    public long SymbolCount { get; set; }
    public long HoldingCount { get; set; }
    public decimal TradeVolume { get; set; }
}

public class HomeDataQueryDto
{
    public HomeDataDto GetHomeData { get; set; }
}