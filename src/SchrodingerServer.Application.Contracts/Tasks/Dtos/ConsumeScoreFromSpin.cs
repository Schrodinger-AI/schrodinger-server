namespace SchrodingerServer.Tasks.Dtos;

public class ConsumeScoreFromSpin
{
    public decimal Score { get; set; }
}

public class ConsumeScoreFromSpinDtoQueryDto
{
    public ConsumeScoreFromSpin GetConsumeScoreFromSpin { get; set; }
}