using System.Collections.Generic;
using SchrodingerServer.Dtos.Cat;

namespace SchrodingerServer.Cat.Provider.Dtos;

public class SchrodingerIndexerStrayCatsDto
{
    public long TotalCount { get; set; }
    public List<StrayCatDto> Data { get; set; }
}

public class SchrodingerIndexerStrayCatsQuery
{
    public SchrodingerIndexerStrayCatsDto GetStrayCats { get; set; }
}
