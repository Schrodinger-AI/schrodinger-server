using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace SchrodingerServer.Dtos.Cat;

public class GetCatListInput : PagedAndSortedResultRequestDto
{
    public const int MaxMaxResultCount = 10000;
    public string ChainId { get; set; }
    public string Address { get; set; }
    public string Tick { get; set; }
    public List<TraitInput> Traits { get; set; }
    public List<int> Generations { get; set; }
    public string Keyword { get; set; }
    public List<string> Rarities { get; set; } = new();
    public string SearchAddress { get; set; }
    public bool FilterSgr { get; set; } = false;
}

public class TraitInput
{
    public string TraitType { get; set; }
    public List<string> Values { get; set; }
}