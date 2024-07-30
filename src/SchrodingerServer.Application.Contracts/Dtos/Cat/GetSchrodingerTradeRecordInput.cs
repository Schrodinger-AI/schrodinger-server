using System;

namespace SchrodingerServer.Dtos.Cat;

public class GetSchrodingerTradeRecordInput
{
    public string ChainId { get; set; }
    public string Symbol { get; set; }
    public string Buyer { get; set; }
    public DateTime TradeTime { get; set; }
}