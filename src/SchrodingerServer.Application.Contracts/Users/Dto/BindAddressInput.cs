using System.ComponentModel.DataAnnotations;

namespace SchrodingerServer.Users.Dto;

public class BindAddressInput
{
    [Required] public string AelfAddress { get; set; }
    [Required] public string EvmAddress { get; set; }
    [Required] public string Signature { get; set; }
    [Required] public string PublicKey { get; set; }
}