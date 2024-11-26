namespace SchrodingerServer.Grains.Grain.Synchronize;

[GenerateSerializer]
public class SyncPendingListDto
{
    [Id(0)]
    public List<SyncPendingDto> List { get; set; }
}
[GenerateSerializer]
public class SyncPendingDto
{
    [Id(0)]
    public string TransactionId { get; set; }
    [Id(1)]
    public string ChainId { get; set; }
}