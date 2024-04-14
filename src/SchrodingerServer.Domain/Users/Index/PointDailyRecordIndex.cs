using System;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.Users.Index;

public class PointDailyRecordIndex : SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public string ChainId { get; set; }
    
    [Keyword] public string PointName { get; set; }
    
    [Keyword] public string BizDate { get; set; }
    
    //contract invoke bizId
    [Keyword] public string BizId { get; set; }
    
    [Keyword] public string Address { get; set; }
    
    [Keyword] public string Status { get; set; }
    
    public decimal PointAmount { get; set; }
    
    public DateTime CreateTime { get; set; }
    
    public DateTime UpdateTime { get; set; }
}