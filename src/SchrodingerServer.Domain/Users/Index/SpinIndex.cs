using System;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.Users.Index;

public class SpinIndex : SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public override string Id { get; set; } 
    [Keyword] public string Address { get; set; }
    [Keyword] public string Seed { get; set; }
    [Keyword] public string Signature { get; set; }
    public SpinStatus Status { get; set; }
    public decimal ConsumeScore { get; set; }
    public long ExpiredTime { get; set; }
    public long CreatedTime { get; set; }
}

public enum SpinStatus
{
    Created,
    Finished,
    Expired
}