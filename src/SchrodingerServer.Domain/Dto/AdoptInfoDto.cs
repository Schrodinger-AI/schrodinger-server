using System;
using System.Collections.Generic;

namespace SchrodingerServer.Dto;

public class AdpotInfoDto
{
    public string AdoptId { get; set; }
    public string Parent { get; set; }
    public string Ancestor { get; set; }
    public string Symbol { get; set; }
    public string Issuer { get; set; }
    public string Owner { get; set; }
    public string Deployer { get; set; }
    public string Adopter { get; set; }
    public string TokenName { get; set; }

    public List<Trait> Attributes { get; set; }

    public Dictionary<string, string> AdoptExternalInfo { get; set; } = new();
    public long InputAmount { get; set; }
    public long LossAmount { get; set; }
    public long CommissionAmount { get; set; }
    public long OutputAmount { get; set; }
    public int ImageCount { get; set; }
    public long TotalSupply { get; set; }
    public int IssueChainId { get; set; }
    public int Gen { get; set; }
    public int ParentGen { get; set; }
    public int Decimals { get; set; }
    public DateTime AdoptTime { get; set; }
    public string Rarity { get; set; }
}

public class Trait
{
    public string TraitType { get; set; }
    public string Value { get; set; }
    public string Percent { get; set; }
    public bool IsRare { get; set; }
}

public class AdoptInfoQuery
{
    public AdpotInfoDto GetAdoptInfo { get; set; }
}


public class AdoptInfoByTimeQuery
{
    public List<AdpotInfoDto> GetAdoptInfoByTime { get; set; }
}





