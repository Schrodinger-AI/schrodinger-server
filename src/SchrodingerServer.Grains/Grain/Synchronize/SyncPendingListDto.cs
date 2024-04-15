namespace SchrodingerServer.Grains.Grain.Synchronize;

public class SyncPendingListDto
{
    public List<SyncPendingDto> List { get; set; }
}

public class SyncPendingDto
{
    public string TransactionId { get; set; }
    public string ChainId { get; set; }
}