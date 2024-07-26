using System.Collections.Generic;
using SchrodingerServer.Dtos.Adopts;

namespace SchrodingerServer.Adopts.provider;

public class AdoptInfo
{
    public string Symbol { get; set; }
    public string TokenName { get; set; }
    public List<Attribute> Attributes { get; set; }
    public string Adopter { get; set; }
    public int ImageCount { get; set; }
    public int Generation { get; set; }
}