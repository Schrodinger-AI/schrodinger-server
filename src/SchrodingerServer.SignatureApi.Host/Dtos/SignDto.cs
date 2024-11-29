using System.ComponentModel.DataAnnotations;

namespace SchrodingerServer.SignatureServer.Dtos;

public class SignDto
{
    [Required] public string ApiKey { get; set; }
    [Required] public string PlainText { get; set; }
}