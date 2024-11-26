using SchrodingerServer.Grains.Grain.Points;

namespace SchrodingerServer.Grains.State.Points;

[GenerateSerializer]
public class PointDailyRecordState : PointDailyRecordGrainBase
{
    [Id(0)]
    public HashSet<string> HolderBalanceIds { get; set; } = new ();

    public void AddHolderBalanceId(string holderBalanceId)
    {
        HolderBalanceIds.Add(holderBalanceId);
    }
}