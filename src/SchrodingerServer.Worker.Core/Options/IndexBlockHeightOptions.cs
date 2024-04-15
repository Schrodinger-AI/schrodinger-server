namespace SchrodingerServer.Worker.Core.Options;

public class IndexBlockHeightOptions
{
    public int SearchTimer { get; set; } = 10;
    public string TargetChainId { get; set; } = "AELF";
    public string SourceChainId { get; set; } = "tDVV";
    public string IndexBlockHeightGrainId { get; set; } = "IndexBlockHeight";
}