namespace SchrodingerServer.Grains.State.Sync;

[GenerateSerializer]
public class SyncState
{
    [Id(0)]
    public string Id { get; set; }
    [Id(1)]
    public string Symbol { get; set; }
    [Id(2)]
    public string TransactionId { get; set; }
    [Id(3)]
    public long ValidateTokenHeight { get; set; }
    [Id(4)]
    public long MainChainIndexHeight { get; set; }
    [Id(5)]
    public string ValidateTokenTxId { get; set; }
    [Id(6)]
    public string ValidateTokenTx { get; set; }
    [Id(7)]
    public string CrossChainCreateTokenTxId { get; set; }
    [Id(8)]
    public string Message { get; set; }
    [Id(9)]
    public string Status { get; set; }
}