namespace SchrodingerServer.Grains.State.Faucets;

public class FaucetsState
{
    public string Id { get; set; }
    public string Address { get; set; }
    public long Amount { get; set; }
    public string Symbol { get; set; }
    public string TransactionId { get; set; }
    public bool Mined { get; set; }
}