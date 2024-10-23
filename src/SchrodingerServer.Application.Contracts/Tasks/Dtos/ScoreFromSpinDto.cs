namespace SchrodingerServer.Tasks.Dtos;

public class ScoreFromSpinDto
{
    public decimal Score { get; set; }
}

public class ScoreFromSpinDtoQueryDto
{
    public ScoreFromSpinDto GetScoreFromSpin { get; set; }
}