namespace SchrodingerServer.Grains.State.Sync;

[GenerateSerializer]
public class SyncPendingState
{
    [Id(0)]
    public List<string> SyncPendingList { get; set; }
}