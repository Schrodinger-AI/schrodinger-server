namespace SchrodingerServer.Grains.State.Sync;

[GenerateSerializer]
public class IndexBlockHeightGrainState
{
    [Id(0)]
    public long SideChainIndexHeight { get; set; }
    [Id(1)]
    public long MainChainIndexHeight { get; set; }
}