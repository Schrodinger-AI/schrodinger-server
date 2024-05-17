using System;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.Users.Index;

public class AddressRelationshipIndex : SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public override string Id { get; set; } 
    [Keyword] public string AelfAddress { get; set; }
    [Keyword] public string EvmAddress { get; set; }
    public DateTime CreatedTime { get; set; }
}