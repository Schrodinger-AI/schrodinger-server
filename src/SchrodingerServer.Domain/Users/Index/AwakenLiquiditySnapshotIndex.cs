using System;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.Users.Index;

public class AwakenLiquiditySnapshotIndex : SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public string Address { get; set; }
    
    [Keyword] public string PointName { get; set; }
    
    [Keyword] public string BizDate { get; set; }
    
    public long Token0Amount { get; set; }
    
    public long Token1Amount { get; set; }
    
    public string Token0Name { get; set; }
    
    public string Token1Name { get; set; }
    
    public DateTime CreateTime { get; set; }
}