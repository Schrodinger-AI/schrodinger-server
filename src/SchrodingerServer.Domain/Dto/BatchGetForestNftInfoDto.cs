using System.Collections.Generic;

namespace SchrodingerServer.Dto;

public class BatchGetForestNftInfoDto
{
    public string Code { get; set; }
    public NftInfoList Data { get; set; }
    public string Message { get; set; }
}

public class NftInfoList
{
    public long TotalCount { get; set; }
    public List<NftInfo> Items { get; set; }
}

public class NftInfo
{
    public string NftSymbol { get; set; }
    public string TokenName { get; set; }
    public decimal ListingPrice { get; set; }   
}