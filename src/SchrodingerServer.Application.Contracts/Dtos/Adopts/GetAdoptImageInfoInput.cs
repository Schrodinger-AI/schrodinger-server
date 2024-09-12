using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace SchrodingerServer.Dtos.Adopts;

public class GetAdoptImageInfoInput
{
    [Required] public string AdoptId { get; set; }
    public string TransactionHash { get; set; }
    public bool AdoptOnly { get; set; } = false;
    [CanBeNull] public string Address { get; set; }
}