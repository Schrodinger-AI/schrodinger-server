using System.Collections.Generic;
using JetBrains.Annotations;
using SchrodingerServer.Dtos.Cat;

namespace SchrodingerServer.Cat.Provider.Dtos;


public class SchrodingerIndexerBoxListDto
{
    public long TotalCount { get; set; }
    public List<SchrodingerIndexerBoxDto> Data { get; set; }
}


public class SchrodingerIndexerBoxDto
{
    public string Symbol { get; set; }
    public string TokenName { get; set; }
    public string AdoptId { get; set; }
    public long Amount { get; set; }
    public int Gen { get; set; }
    public int Decimals { get; set; }
    [CanBeNull] public string Adopter { get; set; }
    public long AdoptTime { get; set; }
    public string Rarity { get; set; }
    public long ConsumeAmount { get; set; }
    public bool DirectAdoption { get; set; }
    public int Rank { get; set; }
    public List<TraitDto> Traits { get; set; }
}


public class SchrodingerIndexerBoxListQuery
{
    public SchrodingerIndexerBoxListDto GetBlindBoxList { get; set; }
}


public class SchrodingerIndexerBoxDetailQuery
{
    public SchrodingerIndexerBoxDto GetBlindBoxDetail { get; set; }
}


