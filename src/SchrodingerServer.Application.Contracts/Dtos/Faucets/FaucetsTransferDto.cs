using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SchrodingerServer.Dtos.Faucets;

public class FaucetsTransferDto : IValidatableObject
{
    [Required] public string Address { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(Address)) yield return new ValidationResult($"Invalid address {Address}.");
    }
}