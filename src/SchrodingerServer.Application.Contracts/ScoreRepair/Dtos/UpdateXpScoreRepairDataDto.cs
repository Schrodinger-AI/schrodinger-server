namespace SchrodingerServer.ScoreRepair.Dtos;

public class UpdateXpScoreRepairDataDto
{
    public string UserId { get; set; }
    public decimal RawScore { get; set; }
    public decimal ActualScore { get; set; }
}