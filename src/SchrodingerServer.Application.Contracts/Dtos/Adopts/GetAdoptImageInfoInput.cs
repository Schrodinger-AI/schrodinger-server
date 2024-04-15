using System.ComponentModel.DataAnnotations;

namespace SchrodingerServer.Dtos.Adopts;

public class GetAdoptImageInfoInput
{
    [Required] public string AdoptId { get; set; }
}