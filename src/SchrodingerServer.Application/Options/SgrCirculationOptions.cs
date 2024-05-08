namespace SchrodingerServer.Options;

public class SgrCirculationOptions
{
    public string EthApiUrl { get; set; }
    public string EthApiKey { get; set; }
    public string SgrContractAddress { get; set; }
    public string AccountAddress { get; set; }
    public int CacheExpiredTtl { get; set; }
    
    public string TotalSupply { get; set; }
    public string Surplus{ get; set; }
    public  string AelfSideChainBalance { get; set; }
}