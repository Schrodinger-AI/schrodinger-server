using System.Collections.Generic;

namespace SchrodingerServer.Cat.Provider.Dtos;

public class RarityDataDto
{
    public List<RarityInfo> RarityInfo { get; set; }
}

public class RarityInfo
{
    public string Symbol { get; set; }
    public int Rank { get; set; }
    public int Generation { get; set; }
    public string AdoptId { get; set; }
    public long OutputAmount { get; set; }
    public string Adopter { get; set; }
}

public class RarityDataDtoQuery
{
    public RarityDataDto GetRarityData { get; set; }
}
