using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace SchrodingerServer.Dtos.Cat;

public class GetCatLatestListInput : PagedAndSortedResultRequestDto
{
    public string ChainId { get; set; }
    public List<string> BlackList { get; set; }
}