namespace SchrodingerServer.Dtos.Cat;

public class HoldingPointBySymbolDto
{
    public decimal Point { get; set; }
    public string Level { get; set; }
}


public class HoldingPointBySymbolQueryDto
{
    public HoldingPointBySymbolDto GetHoldingPointBySymbol { get; set; }
}