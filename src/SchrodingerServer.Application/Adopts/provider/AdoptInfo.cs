using System;
using System.Collections.Generic;
using Attribute = SchrodingerServer.Dtos.Adopts.Attribute;

namespace SchrodingerServer.Adopts.provider;

public class AdoptInfo
{
    public string Symbol { get; set; }
    public string TokenName { get; set; }
    public List<Attribute> Attributes { get; set; }
    public string Adopter { get; set; }
    public int ImageCount { get; set; }
    public int Generation { get; set; }
    public string Rarity { get; set; }
    public DateTime AdoptTime { get; set; }
}