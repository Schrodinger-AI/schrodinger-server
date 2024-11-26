namespace SchrodingerServer.Grains.State.ZealyScore;

[GenerateSerializer]
public class ZealyUserXpState
{
    // search record and send , es->add
    [Id(0)]
    public List<RecordInfo> RecordInfos { get; set; } = new();
    [Id(1)]
    public string Id { get; set; }
    [Id(2)]
    public string Address { get; set; }
    [Id(3)]
    public decimal LastXp { get; set; }
    [Id(4)]
    public decimal CurrentXp { get; set; }
    [Id(5)]
    public DateTime CreateTime { get; set; }
    [Id(6)]
    public DateTime UpdateTime { get; set; }
    [Id(7)]
    public bool IsRollback { get; set; }
}

[GenerateSerializer]
public class RecordInfo
{
    [Id(0)]
    public string Date { get; set; }
    [Id(1)]
    public decimal CurrentXp { get; set; }
    [Id(2)]
    public decimal IncreaseXp { get; set; }
    [Id(3)]
    public decimal PointsAmount { get; set; }
}