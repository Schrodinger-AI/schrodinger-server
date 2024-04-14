namespace SchrodingerServer.Point;

public class TokenExchangeDto
{
    
    public string FromSymbol { get; set; }
    public string ToSymbol { get; set; }
    public decimal Exchange { get; set; }
    public long Timestamp { get; set; }


    public static TokenExchangeDto One(string fromSymbol, string toSymbol, long timestamp)
    {
        return new TokenExchangeDto
        {
            FromSymbol = fromSymbol,
            ToSymbol = toSymbol,
            Exchange = 1,
            Timestamp = timestamp
        };
    }
    
}