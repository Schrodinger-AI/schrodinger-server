using System.Collections.Generic;

namespace SchrodingerServer.Image;

public class GetImageResponse
{
    public AdoptInfo AdoptInfo { get; set; }
    public string Image { get; set; }
}

public class AdoptInfo
{
    public int Generation { get; set; }
    public List<Trait> Traits { get; set; }
}

public class Trait
{
    public string TraitType { get; set; }
    public string Value { get; set; }
    public double Percent { get; set; }
}