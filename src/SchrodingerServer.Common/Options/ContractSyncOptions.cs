namespace SchrodingerServer.Common.Options;

public class ContractSyncOptions
{
    public int Sync { get; set; }

    public int Limit { get; set; } = 100;
}