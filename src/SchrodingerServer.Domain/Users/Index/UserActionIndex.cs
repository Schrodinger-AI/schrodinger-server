using System;
using System.Collections.Generic;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.Users.Index;

public class UserActionIndex : SchrodingerEntity<Guid>, IIndexBuild
{
    
    public List<UserActionItem> Actions = new ();
}

public class UserActionItem
{
    
    [Keyword] public string ActionType { get; set; }
    public long ActionTime { get; set; }
    
}