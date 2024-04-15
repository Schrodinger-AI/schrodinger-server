namespace SchrodingerServer.Grains.State.Sync;

public class IndexBlockHeightGrainState
{
    public long SideChainIndexHeight { get; set; }
    public long MainChainIndexHeight { get; set; }
}