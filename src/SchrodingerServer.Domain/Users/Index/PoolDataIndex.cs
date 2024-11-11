using System.Collections.Generic;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.Users.Index;

public class PoolDataIndex : SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public override string Id { get; set; } 
    [Keyword] public string PoolId { get; set; }
    [Keyword] public string WinnerAddress { get; set; }
    [Keyword] public string WinnerSymbol { get; set; }
    public long Balance { get; set; }
    public Dictionary<string, string> ExtraData { get; set; } = new();
    public long CreatedTime { get; set; }
    public long UpdateTime { get; set; }
}