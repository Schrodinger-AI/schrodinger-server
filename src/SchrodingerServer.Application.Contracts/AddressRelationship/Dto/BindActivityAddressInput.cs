using System.ComponentModel.DataAnnotations;

namespace SchrodingerServer.AddressRelationship.Dto;

public class BindActivityAddressInput
{
    [Required] public string AelfAddress { get; set; }
    [Required] public string SourceChainAddress { get; set; }
    [Required] public string ActivityId { get; set; }
    [Required] public string Signature { get; set; }
    [Required] public string PublicKey { get; set; }
}