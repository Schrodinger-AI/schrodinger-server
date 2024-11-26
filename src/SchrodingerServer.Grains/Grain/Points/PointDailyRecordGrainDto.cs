namespace SchrodingerServer.Grains.Grain.Points;

[GenerateSerializer]
public class PointDailyRecordGrainDto : PointDailyRecordGrainBase
{
    [Id(0)]
    public string HolderBalanceId { get; set; }
}