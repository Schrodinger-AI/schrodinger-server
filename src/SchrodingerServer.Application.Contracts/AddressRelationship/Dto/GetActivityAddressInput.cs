using System.ComponentModel.DataAnnotations;

namespace SchrodingerServer.AddressRelationship.Dto;

public class GetActivityAddressInput
{
    [Required]public string AelfAddress { get; set; }
    [Required]public string ActivityId { get; set; }
}