using System.Collections.Generic;

namespace SchrodingerServer.Dtos.Cat;

public class StrayCatsListDto
{
    public long TotalCount { get; set; }
    public List<StrayCatDto> Data { get; set; }
}

public class StrayCatDto
{
    public string AdoptId { get; set; }
    public string InscriptionImageUri { get; set; }
    public string TokenName { get; set; }
    public int Gen { get; set; }
    public string Symbol { get; set; }
    public long ConsumeAmount { get; set; }
    public long ReceivedAmount { get; set; }
    public int Decimals { get; set; }
    public List<StrayCatTraitsDto> ParentTraits { get; set; }
    public string NextTokenName { get; set; }
    public string NextSymbol { get; set; }
    public long NextAmount{ get; set; }
    public bool DirectAdoption { get; set; }
    
}

public class StrayCatTraitsDto
{
    public string TraitType { get; set; }
    public string Value { get; set; }
}