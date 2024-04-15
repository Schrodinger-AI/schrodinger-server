namespace SchrodingerServer.Grains.State.Sync;

public class SyncState
{
    public string Id { get; set; }
    public string Symbol { get; set; }
    public string TransactionId { get; set; }
    public long ValidateTokenHeight { get; set; }
    public long MainChainIndexHeight { get; set; }
    public string ValidateTokenTxId { get; set; }
    public string ValidateTokenTx { get; set; }
    public string CrossChainCreateTokenTxId { get; set; }
    public string Message { get; set; }
    public string Status { get; set; }
}