using System;
using System.Collections.Generic;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.Users.Index;

public class TgBotLogIndex : SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public override string Id { get; set; } 
    [Keyword] public string Address { get; set; }
    public string UserId { get; set; }
    public string Username { get; set; }
    public string From { get; set; }
    public string Language { get; set; }
    public long LoginTime { get; set; } 
    public long RegisterTime { get; set; }
    public decimal Score { get; set; }
    public Dictionary<string, string> ExtraData { get; set; } = new();
}