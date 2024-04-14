using SchrodingerServer.Grains.State.ZealyScore;

namespace SchrodingerServer.Grains.Grain.ZealyScore.Dtos;

public class ZealyUserXpGrainDto
{
    public string Id { get; set; }
    public string Address { get; set; }

    public decimal LastXp { get; set; }
    public decimal CurrentXp { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public bool IsRollback { get; set; }
    public List<RecordInfo> RecordInfos { get; set; } = new();
}