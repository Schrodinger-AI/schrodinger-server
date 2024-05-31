using System;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.Users.Index;

public class ActivityAddressIndex : SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public override string Id { get; set; } 
    [Keyword] public string ActivityId { get; set; }
    [Keyword] public string AelfAddress { get; set; }
    [Keyword] public string SourceChainAddress { get; set; }
    [Keyword] public ChainType SourceChainType { get; set; }
    public DateTime CreatedTime { get; set; }
}

public enum ChainType
{
    Undefined,
    EVM,
    SOLANA,
    BTC
}