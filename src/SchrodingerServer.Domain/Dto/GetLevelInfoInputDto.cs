using System.Collections.Generic;

namespace SchrodingerServer.Dto;

public class GetLevelInfoInputDto
{
    
    public string Address { get; set; }
    public string SearchAddress { get; set; } = "";
    public LinkedList<LinkedList<LinkedList<LinkedList<string>>>> CatsTraits { get; set; }
}