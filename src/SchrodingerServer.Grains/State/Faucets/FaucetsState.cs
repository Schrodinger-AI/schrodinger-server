namespace SchrodingerServer.Grains.State.Faucets;

[GenerateSerializer]
public class FaucetsState
{
    [Id(0)]
    public string Id { get; set; }
    [Id(1)]
    public string Address { get; set; }
    [Id(2)]
    public long Amount { get; set; }
    [Id(3)]
    public string Symbol { get; set; }
    [Id(4)]
    public string TransactionId { get; set; }
    [Id(5)]
    public bool Mined { get; set; }
}