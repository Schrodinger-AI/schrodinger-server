using System;
using System.Collections.Generic;

namespace SchrodingerServer.Message.Dtos;

public class MessageListDto
{
    public long TotalCount { get; set; }
    public List<MessageInfo> Data { get; set; }
}

public class MessageInfo
{
    public string NftInfoId { get; set; }
    public string TokenName { get; set; }
    public string PreviewImage { get; set; }
    
    public string From { get; set; }
    
    public string To { get; set; }
    
    public long Amount { get; set; }
    
    public decimal Price { get; set; }
    
    public long Createtime { get; set; }

    public int Generation { get; set; }
    public int Decimals { get; set; }
    public string AwakenPrice{ get; set; }
    public string Level { get; set; }
    public string Rarity { get; set; }
    public int Rank { get; set; }
    public string Grade { get; set; }
    public string Star { get; set; }
    public string Describe{ get; set; }
}
