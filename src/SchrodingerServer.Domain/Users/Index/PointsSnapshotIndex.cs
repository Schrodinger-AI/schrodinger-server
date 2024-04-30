using System;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.Users.Index;

public class PointsSnapshotIndex : SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public string Address { get; set; }
    
    [Keyword] public string PointName { get; set; }
    
    [Keyword] public string BizDate { get; set; }
    
    public decimal Amount { get; set; }
    
    public DateTime CreateTime { get; set; }
}