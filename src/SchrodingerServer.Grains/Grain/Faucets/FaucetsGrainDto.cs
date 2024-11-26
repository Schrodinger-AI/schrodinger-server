namespace SchrodingerServer.Grains.Grain.Faucets;

[GenerateSerializer]
public class FaucetsGrainDto
{
    [Id(0)]
    public string Address { get; set; }
    [Id(1)]
    public long Amount { get; set; }
    [Id(2)]
    public string Symbol { get; set; }
    [Id(3)]
    public string TransactionId { get; set; }
}