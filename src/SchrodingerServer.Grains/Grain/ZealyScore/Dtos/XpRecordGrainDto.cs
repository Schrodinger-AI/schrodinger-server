namespace SchrodingerServer.Grains.Grain.ZealyScore.Dtos;

[GenerateSerializer]
public class XpRecordGrainDto
{
    [Id(0)]
    public string Id { get; set; }
    [Id(1)]
    public string UserId { get; set; }
    [Id(2)]
    public string Address { get; set; }
    [Id(3)]
    public decimal CurrentXp { get; set; }
    [Id(4)]
    public decimal IncreaseXp { get; set; }

    // xp * coefficient
    [Id(5)]
    public decimal PointsAmount { get; set; }
    [Id(6)]
    public string BizId { get; set; }
    [Id(7)]
    public string Status { get; set; }
    [Id(8)]
    public long CreateTime { get; set; }
    [Id(9)]
    public long UpdateTime { get; set; }
    [Id(10)]
    public string Remark { get; set; }
}