using System.Collections.Generic;

namespace SchrodingerServer.ScoreRepair.Dtos;

public class XpRecordPageResultDto
{
    public List<XpRecordDto> Data { get; set; }
    public long TotalCount { get; set; }
}
public class XpRecordDto
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string Address { get; set; }
    public decimal CurrentXp { get; set; }
    public decimal IncreaseXp { get; set; }

    // xp * coefficient
    public decimal PointsAmount { get; set; }
    public string BizId { get; set; }
    public string Status { get; set; }
    public long CreateTime { get; set; }
    public long UpdateTime { get; set; }
    public string Remark { get; set; }
}