namespace SchrodingerServer.Grains.Grain.Synchronize;

public class SyncGrainDto
{
    public string Status { get; set; }
    public string TransactionId { get; set; }
}

public class SyncJobGrainDto
{
    public string Id { get; set; }
}