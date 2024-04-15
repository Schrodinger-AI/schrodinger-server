using System.Collections.Generic;

namespace SchrodingerServer.ScoreRepair.Dtos;

public class XpScoreRepairDataPageDto
{
    public List<XpScoreRepairDataDto> Data { get; set; }
    public long TotalCount { get; set; }
}
public class XpScoreRepairDataDto
{
    public string UserId { get; set; }
    public decimal LastRawScore { get; set; }
    public decimal LastActualScore { get; set; }
    public decimal RawScore { get; set; }
    public decimal ActualScore { get; set; }
    public long CreateTime { get; set; }
    public long UpdateTime { get; set; }
}