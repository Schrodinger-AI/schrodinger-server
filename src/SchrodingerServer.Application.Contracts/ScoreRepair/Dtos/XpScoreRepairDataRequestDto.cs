using Volo.Abp.Application.Dtos;

namespace SchrodingerServer.ScoreRepair.Dtos;

public class XpScoreRepairDataRequestDto : PagedResultRequestDto
{
    public string UserId { get; set; }
}