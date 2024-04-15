using System.Collections.Generic;

namespace SchrodingerServer.Dtos.Cat;

public class SchrodingerListDto
{
    public long TotalCount { get; set; }
    public List<SchrodingerDto> Data { get; set; }
}

public class SchrodingerDto
{
    public string Symbol { get; set; }
    public string TokenName { get; set; }
    public string InscriptionImageUri { get; set; }
    public long Amount { get; set; }
    public int Generation { get; set; }
    public int Decimals { get; set; }
    public string InscriptionDeploy { get; set; }
    public string Adopter { get; set; }
    public long AdoptTime { get; set; }
    public string Tick { get; set; }
    public List<TraitsInfo> Traits { get; set; }
    
    public string AwakenPrice{ get; set; }
    
    public string Level { get; set; }
    public string Rarity { get; set; }
    public int Rank { get; set; }
    public string Total { get; set; }
    public string Describe{ get; set; }
    public string Token { get; set; }
    public string Address { get; set; }
}

public class TraitsInfo
{
    public string TraitType { get; set; }
    public string Value { get; set; }
}