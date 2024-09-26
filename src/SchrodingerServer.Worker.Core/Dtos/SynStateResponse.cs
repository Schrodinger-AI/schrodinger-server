namespace SchrodingerServer.Worker.Core.Dtos;

public class SynStateResponse
{
    public VersionData CurrentVersion { get; set; }
    public VersionData PendingVersion { get; set; }
}

public class VersionData
{
    public string Version { get; set; }
    public List<ChainData> Items { get; set; }
}

public  class ChainData
{
    public string ChainId { get; set; }
    public string LongestChainBlockHash { get; set; }
    public long LongestChainHeight { get; set; }
    public string BestChainBlockHash { get; set; }
    public long BestChainHeight { get; set; }
    public string LastIrreversibleBlockHash { get; set; }
    public long LastIrreversibleBlockHeight { get; set; }
}