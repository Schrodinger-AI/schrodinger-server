using System;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.ContractInvoke.Index;

public class ContractInvokeIndex : SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public string ChainId { get; set; }
    [Keyword] public string BizId { get; set; }

    [Keyword] public string BizType { get; set; }
    
    [Keyword] public string ContractAddress { get; set; }
    
    [Keyword] public string ContractMethod { get; set; }
    
    [Keyword] public string Sender { get; set; }
    
    public string ParamJson { get; set; }
    
    [Keyword] public string Param { get; set; }
    
    [Keyword] public string TransactionId { get; set; }
    [Keyword] public string Status { get; set; } 
    
    [Keyword] public string TransactionStatus { get; set; }

    [Keyword] public string Message { get; set; }

    public long BlockHeight { get; set; }
    
    public int RetryCount { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }
}