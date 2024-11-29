using System.ComponentModel.DataAnnotations;

namespace SchrodingerServer.SignatureServer.Dtos;

public class SignResponseDto
{
    public string Signature { get; set; }
}

public class SendSignatureDto
{
    [Required] public string PublicKey { get; set; }
    [Required] public string HexMsg { get; set; }
}