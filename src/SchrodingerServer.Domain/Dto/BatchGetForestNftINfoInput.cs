using System.Collections.Generic;

namespace SchrodingerServer.Dto;

public class BatchGetForestNftINfoInput
{
    public string CollectionId { get; set; }
    public string CollectionType { get; set; }
    public string Sorting { get; set; }
    public string SearchParam { get; set; }
    public List<string> ChainList { get; set; }
    public List<string> SymbolTypeList { get; set; }
    public bool HasListingFlag { get; set; } = false;
    public bool HasAuctionFlag { get; set; } = false;
    public bool HasOfferFlag { get; set; } = false;
    public long SkipCount { get; set; }
    public long MaxResultCount { get; set; }
    public List<string> NFTIdList { get; set; }
}