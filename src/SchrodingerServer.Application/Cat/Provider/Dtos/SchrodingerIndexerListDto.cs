using System.Collections.Generic;
using SchrodingerServer.Dtos.Cat;

namespace SchrodingerServer.Cat.Provider.Dtos;

public class SchrodingerIndexerListDto
{
    public long TotalCount { get; set; }
    public List<SchrodingerIndexerDto> Data { get; set; }
}

public class SchrodingerIndexerDto
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
    public string Address { get; set; }
}


public class SchrodingerIndexerQuery
{
    public SchrodingerIndexerListDto GetSchrodingerList { get; set; }
}

public class SchrodingerIndexerLatestQuery
{
    public SchrodingerIndexerListDto GetLatestSchrodingerListAsync { get; set; }
}
