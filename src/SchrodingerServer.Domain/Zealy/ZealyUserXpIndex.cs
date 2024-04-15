using System;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.Zealy;

public class ZealyUserXpIndex : SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public override string Id { get; set; }
    [Keyword] public string Address { get; set; }
    public decimal LastXp { get; set; }
    public decimal Xp { get; set; }
    public long UseRepairTime { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public DateTime HandleXpTime { get; set; }
}