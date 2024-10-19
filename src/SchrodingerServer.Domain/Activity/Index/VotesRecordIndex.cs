using System;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.Activity.Index;

public class VotesRecordIndex : SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public override string Id { get; set; } 
    [Keyword] public string Address { get; set; }
    [Keyword] public string AdoptId { get; set; }
    public int Faction { get; set; }
    public DateTime CreatedTime { get; set; }
}