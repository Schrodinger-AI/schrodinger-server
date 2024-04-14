using System;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;
using System.Collections.Generic;

namespace SchrodingerServer.Users.Index;

public class UserExtraIndex : SchrodingerEntity<Guid>, IIndexBuild
{
    [Keyword] public string UserName { get; set; }
    [Keyword] public string AelfAddress { get; set; }
    [Keyword] public string CaHash { get; set; }
    [Keyword] public string CaAddressMain { get; set; }
    [Nested(Name = "CaAddressListSide",Enabled = true,IncludeInParent = true,IncludeInRoot = true)]
    public List<UserAddress> CaAddressListSide { get; set; }
    [Keyword] public string? RegisterDomain { get; set; }
    public DateTime CreateTime { get; set; }
}