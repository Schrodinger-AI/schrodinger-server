using System.Collections.Generic;

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
    public string Adopter { get; set; }
    public long AdoptTime { get; set; }
    public string Rarity { get; set; }
    public int Rank { get; set; }
}



public class SchrodingerIndexerBoxQuery
{
    public SchrodingerIndexerBoxListDto GetBlindBoxList { get; set; }
}