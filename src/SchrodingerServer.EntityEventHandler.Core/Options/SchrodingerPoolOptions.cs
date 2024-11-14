namespace SchrodingerServer.EntityEventHandler.Core.Options;

public class SchrodingerPoolOptions
{
    public long BeginTs { get; set; }
    public long TargetRank { get; set; }
    public string PoolAddress { get; set; }
    public string TokenContractAddress { get; set; }
    public string PublicKey { get; set; }
    public string ChainId { get; set; }
}