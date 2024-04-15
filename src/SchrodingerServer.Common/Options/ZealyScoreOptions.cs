namespace SchrodingerServer.Common.Options;

public class ZealyScoreOptions
{
    public string ChainId { get; set; } = CommonConstant.TDVVChainId;
    public string PointName { get; set; } = CommonConstant.ZealyPointName;
    public decimal Coefficient { get; set; }
    public int FetchCount { get; set; } = 2000;
}