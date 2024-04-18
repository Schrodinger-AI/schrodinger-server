using System.Collections.Generic;

namespace SchrodingerServer.Dtos.Cat;

public class SchrodingerDetailDto
{
    public string Symbol { get; set; }
    public string TokenName { get; set; }
    public string InscriptionImageUri { get; set; }
    public long Amount { get; set; }
    public int Generation { get; set; }
    public int Decimals { get; set; }
    public string Address { get; set; }
    public List<TraitDto> Traits { get; set; }
}

public class TraitDto
{
    public string TraitType { get; set; }
    public string Value { get; set; }
    public decimal Percent { get; set; }
}
