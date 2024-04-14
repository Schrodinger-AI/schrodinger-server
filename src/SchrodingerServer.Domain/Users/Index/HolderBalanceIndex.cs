using System;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.Users.Index;

public class HolderBalanceIndex : SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public string ChainId { get; set; }

    [Keyword] public string Address { get; set; }
    
    [Keyword] public string BizDate { get; set; }
    
    [Keyword] public string Symbol { get; set; }
    
    public long Balance { get; set; }
    
    public DateTime ChangeTime { get; set; }
}