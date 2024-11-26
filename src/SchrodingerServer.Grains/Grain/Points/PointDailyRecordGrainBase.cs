namespace SchrodingerServer.Grains.Grain.Points;

[GenerateSerializer]
public class PointDailyRecordGrainBase
{
    [Id(0)]
    public string Id { get; set; }

    [Id(1)]
    public string ChainId { get; set; }

    [Id(2)]
    public string PointName { get; set; }

    [Id(3)]
    public string BizDate { get; set; }

    [Id(4)]
    public string BizId { get; set; }

    [Id(5)]
    public string Address { get; set; }

    [Id(6)]
    public decimal PointAmount { get; set; }

    [Id(7)]
    public string Status { get; set; }

    [Id(8)]
    public DateTime CreateTime { get; set; }

    [Id(9)]
    public DateTime UpdateTime { get; set; }
}