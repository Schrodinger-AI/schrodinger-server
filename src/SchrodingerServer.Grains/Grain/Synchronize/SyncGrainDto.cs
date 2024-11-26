namespace SchrodingerServer.Grains.Grain.Synchronize;

[GenerateSerializer]
public class SyncGrainDto
{
    [Id(0)]
    public string Status { get; set; }
    [Id(1)]
    public string TransactionId { get; set; }
}

[GenerateSerializer]
public class SyncJobGrainDto
{
    [Id(0)]
    public string Id { get; set; }
}