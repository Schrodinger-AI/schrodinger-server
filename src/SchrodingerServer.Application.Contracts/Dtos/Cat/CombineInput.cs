using System.Collections.Generic;

namespace SchrodingerServer.Dtos.Cat;

public class CombineInput
{
    public List<string> Symbols { get; set; }
    public string Address { get; set; }
}