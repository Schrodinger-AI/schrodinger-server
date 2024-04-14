namespace SchrodingerServer.Options;

public class PointContractOptions
{
    public string ChainId { get; set; }
    public string BaseUrl { get; set; }
    public string ContractAddress { get; set; }
    public string CommonPrivateKeyForCallTx { get; set; }
}