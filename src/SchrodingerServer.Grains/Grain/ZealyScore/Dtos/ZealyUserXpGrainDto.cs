using SchrodingerServer.Grains.State.ZealyScore;

namespace SchrodingerServer.Grains.Grain.ZealyScore.Dtos;

[GenerateSerializer]
public class ZealyUserXpGrainDto
{
    [Id(0)]
    public string Id { get; set; }
    [Id(1)]
    public string Address { get; set; }

    [Id(2)]
    public decimal LastXp { get; set; }
    [Id(3)]
    public decimal CurrentXp { get; set; }
    [Id(4)]
    public DateTime CreateTime { get; set; }
    [Id(5)]
    public DateTime UpdateTime { get; set; }
    [Id(6)]
    public bool IsRollback { get; set; }
    [Id(7)]
    public List<RecordInfo> RecordInfos { get; set; } = new();
}