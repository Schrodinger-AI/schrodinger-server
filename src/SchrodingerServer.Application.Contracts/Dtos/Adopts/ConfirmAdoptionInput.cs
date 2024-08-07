using System.ComponentModel.DataAnnotations;

namespace SchrodingerServer.Dtos.Adopts;

public class ConfirmAdoptionInput
{
    [Required] public string AdoptId { get; set; }
}