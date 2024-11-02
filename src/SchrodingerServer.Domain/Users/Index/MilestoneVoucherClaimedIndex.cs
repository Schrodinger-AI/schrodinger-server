using System;
using System.Collections.Generic;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.Users.Index;

public class MilestoneVoucherClaimedIndex : SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public override string Id { get; set; }
    [Keyword] public string Address { get; set; }
    [Keyword] public string TaskId { get; set; }
    public int Level { get; set; }
    public int Amount { get; set; }
    public long CreatedTime { get; set; }
    public Dictionary<string, string> ExtraData { get; set; } = new();
}