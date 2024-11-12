namespace SchrodingerServer.Common.Options;

public class PoolOptions
{
    public long BeginTs { get; set; }
    public long TargetRank { get; set; }
    public string PoolAddress { get; set; }
    public string TokenContractAddress { get; set; }
    public string PublicKey { get; set; }
    public string ChainId { get; set; }
    public int RankNumber { get; set; } = 3;
    public long Deadline { get; set; } = 1731859200;
    public string PoolId { get; set; } = "pool1";
}