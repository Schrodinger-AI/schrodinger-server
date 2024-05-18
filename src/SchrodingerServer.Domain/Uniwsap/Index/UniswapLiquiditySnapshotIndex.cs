using System;
using System.Runtime.InteropServices.JavaScript;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.Uniswap.Index;

public class UniswapPositionSnapshotIndex : SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public override string Id { get; set; }
    [Keyword] public string PoolId { get; set; }
    [Keyword] public string Token0Symbol { get; set; }
    [Keyword] public string Token1Symbol { get; set; }
    [Keyword] public string PositionOwner { get; set; }
    [Keyword] public string PositionId { get; set; }
    [Keyword] public string CurrentPrice { get; set; }
    [Keyword] public string PositionLowPrice { get; set; }
    [Keyword] public string PositionHighPrice { get; set; }
    
    [Keyword] public string BizDate { get; set; }
    [Keyword] public string PointAmount { get; set; }
    
    public DateTime CreateTime { get; set; }
    
    public Extra ExtraData { get; set; }
    
    public class Extra
    {
        
    }
}