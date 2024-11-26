namespace SchrodingerServer.Grains.Grain.Faucets;

[GenerateSerializer]
public class FaucetsTransferGrainDto
{
    [Id(0)]
    public string Address { get; set; }
}