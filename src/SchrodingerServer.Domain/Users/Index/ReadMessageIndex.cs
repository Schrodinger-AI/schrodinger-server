using System;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.Users.Index;

public class ReadMessageIndex : SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public string Address { get; set; }
    
    [Keyword] public string MessageId { get; set; }
    
    public DateTime CreateTime { get; set; }
}