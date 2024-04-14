namespace SchrodingerServer.Grains.Grain.ApplicationHandler;

public class FaucetsTransferOptions
{
    public string ChainId { get; set; }
    public int FaucetsTransferAmount { get; set; } = 1;
    public string FaucetsTransferSymbol { get; set; }
    public string ManagerAddress { get; set; }
    public int SymbolDecimal { get; set; } = 8;
}

public class SyncTokenOptions
{
    public string TargetChainId { get; set; } = "AELF";
    public string SourceChainId { get; set; } = "tDVV";
    public string IndexBlockHeightGrainId { get; set; } = "IndexBlockHeight";
}