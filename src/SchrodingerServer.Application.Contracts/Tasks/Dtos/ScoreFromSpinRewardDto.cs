namespace SchrodingerServer.Tasks.Dtos;

public class ScoreFromSpinRewardDto
{
    public decimal Score { get; set; }
}

public class ScoreFromSpinDtoRewardQueryDto
{
    public ScoreFromSpinRewardDto GetScoreFromSpinReward { get; set; }
}