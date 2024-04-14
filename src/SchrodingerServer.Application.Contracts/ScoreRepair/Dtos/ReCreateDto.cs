using System.ComponentModel.DataAnnotations;

namespace SchrodingerServer.ScoreRepair.Dtos;

public class ReCreateDto
{
    [Required] public string BizId { get; set; }
}