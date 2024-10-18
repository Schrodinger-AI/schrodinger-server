using JetBrains.Annotations;
using Volo.Abp.Application.Dtos;

namespace SchrodingerServer.Message.Dtos;

public class GetMessageListInput : PagedAndSortedResultRequestDto
{
    public string Address { get; set; }
}

public class GetSchrodingerSoldListInput
{
    public string Address { get; set; }
    public long SkipCount { get; set; }
    public long MaxResultCount { get; set; }
    public string FilterSymbol { get; set; }
    
    public string ChainId { get; set; }
    public long TimestampMin { get; set; }
    public string Buyer { get; set; } = "";
}

