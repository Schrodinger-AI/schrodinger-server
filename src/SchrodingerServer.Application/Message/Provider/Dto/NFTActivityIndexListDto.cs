using System;
using System.Collections.Generic;
using Nest;

namespace SchrodingerServer.Message.Provider.Dto;


public class NFTActivityIndexListDto
{
    public long TotalRecordCount { get; set; }
    public List<NFTActivityIndexDto> Data { get; set; }
}

public  class NFTActivityIndexDto
{
    public string Id { get; set; }
    public string NftInfoId { get; set; }
    
    public NFTActivityType Type { get; set; }
    
    public string From { get; set; }
    
    public string To { get; set; }
    
    public long Amount { get; set; }
    
    public decimal Price { get; set; }
    
    public string TransactionHash { get; set; }
    
    public DateTime Timestamp { get; set; }
}

public class NFTActivityIndexListQueryDto
{
    public NFTActivityIndexListDto GetSchrodingerSoldRecord { get; set; }
}


public class SchrodingerSoldListQueryDto
{
    public List<NFTActivityIndexDto> GetSchrodingerSoldList{ get; set; }
}

public enum NFTActivityType
{
    Issue,
    Burn,
    Transfer,
    Sale,
    ListWithFixedPrice,
    DeList,
    MakeOffer,
    CancelOffer,
    PlaceBid
}

