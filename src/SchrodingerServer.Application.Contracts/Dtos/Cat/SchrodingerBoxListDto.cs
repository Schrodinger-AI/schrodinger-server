using System.Collections.Generic;

namespace SchrodingerServer.Dtos.Cat;

public class SchrodingerBoxListDto
{
    public long TotalCount { get; set; }
    public List<BlindBoxDto> Data { get; set; }
}

public class BlindBoxDto
{
    public string Symbol { get; set; }
    public string TokenName { get; set; }
    public long Amount { get; set; }
    public int Generation { get; set; }
    public int Decimals { get; set; }
    public string Adopter { get; set; }
    public long AdoptTime { get; set; }
    public string Level { get; set; }
    public string Rarity { get; set; }
    public int Rank { get; set; }
    public string Describe { get; set; }
    public string InscriptionImageUri { get; set; }
    public List<TraitDto> Traits { get; set; }
    public string SpecialTrait { get; set; }
}

public class BoxRarityConst
{
    public static List<string> RarityList = new List<string>
    { 
        "Diamond",
        "Emerald",
        "Platinum",
        "Gold",
        "Silver",
        "Bronze"
    };
}