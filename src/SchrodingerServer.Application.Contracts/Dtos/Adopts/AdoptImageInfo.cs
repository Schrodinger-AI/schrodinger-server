using System.Collections.Generic;

namespace SchrodingerServer.Dtos.Adopts;

public class AdoptImageInfo
{
    public int Generation { get; set; }
    public List<Attribute> Attributes { get; set; }
    public List<string> Images { get; set; }
}

public class Attribute
{
    public string TraitType { get; set; }
    public string Value { get; set; }
    public string Percent { get; set; }
}

