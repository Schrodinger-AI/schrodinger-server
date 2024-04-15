using SchrodingerServer.Grains.Grain.Points;

namespace SchrodingerServer.Grains.State.Points;

public class PointDailyRecordState : PointDailyRecordGrainBase
{
    public HashSet<string> HolderBalanceIds { get; set; } = new ();

    public void AddHolderBalanceId(string holderBalanceId)
    {
        HolderBalanceIds.Add(holderBalanceId);
    }
}