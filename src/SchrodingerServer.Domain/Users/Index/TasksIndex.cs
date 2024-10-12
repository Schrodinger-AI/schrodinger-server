using System;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.Users.Index;

public class TasksIndex : SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public override string Id { get; set; } 
    [Keyword] public string TaskId { get; set; }
    [Keyword] public string Address { get; set; }
    [Keyword] public UserTaskStatus Status { get; set; }
    [Keyword] public string Date { get; set; }
    [Keyword] public string Name { get; set; }
    public decimal Score { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

public enum UserTaskStatus
{
    Created,
    Finished,
    Claimed
}


public class TasksScoreIndex : SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public override string Id { get; set; } 
    [Keyword] public string Address { get; set; }
    public decimal Score { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}


public class TasksScoreDetailIndex : SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public override string Id { get; set; } 
    [Keyword] public string Address { get; set; }
    [Keyword] public string TaskId { get; set; }
    public decimal Score { get; set; }
    public DateTime CreatedTime { get; set; }
}