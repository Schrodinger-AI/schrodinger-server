namespace SchrodingerServer.Grains.Grain.Points;

[GenerateSerializer]
public class PointDailyDispatchGrainDto
{
    [Id(0)]
    public string Id { get; set; }

    [Id(1)]
    public string BizDate { get; set; }

    [Id(2)]
    public int Height{ get; set; }

    [Id(3)]
    public DateTime CreateTime { get; set; }

    [Id(4)]
    public bool Executed { get; set; }
    
}