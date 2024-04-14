using System.Collections.Generic;

namespace SchrodingerServer.Options;


public class ExchangeOptions
{
    public GateIoOptions GateIo { get; set; }

    public bool UseUniswap { get; set; } = false; 

}

public class GateIoOptions
{
    public string BaseUrl { get; set; } = "https://api.gateio.ws";

    // standard symbol => GateIo symbol
    public string FromSymbol{ get; set; } = "SGR";
    public string ToSymbol { get; set; } ="USDT";
}