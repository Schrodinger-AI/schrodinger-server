namespace SchrodingerServer.Grains.State.Sync;

[GenerateSerializer]
public class SubscribeState
{
    [Id(0)]
    public long SubscribeHeight { get; set; }
}