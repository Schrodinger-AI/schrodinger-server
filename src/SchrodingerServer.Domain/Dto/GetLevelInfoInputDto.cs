using System.Collections.Generic;

namespace SchrodingerServer.Dto;

public class GetLevelInfoInputDto
{
    
    public string Address { get; set; }
    public string SearchAddress { get; set; } = "";
    public List<List<List<List<string>>>> CatsTraits { get; set; }
    
    public bool IsGen9 { get; set; }
    public string SpecialTag { get; set; } = "";
}