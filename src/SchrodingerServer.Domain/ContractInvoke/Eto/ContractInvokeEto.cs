using System;
using Volo.Abp.EventBus;

namespace SchrodingerServer.ContractInvoke.Eto;

[EventName("ContractInvokeEto")]
public class ContractInvokeEto
{
    public string Id { get; set; }

    public string ChainId { get; set; }

    public string BizId { get; set; }

    public string BizType { get; set; }
    public string ContractAddress { get; set; }
    public string ContractMethod { get; set; }
    public string Sender { get; set; }
    public string ParamJson { get; set; }
    public string Param { get; set; }
    public string TransactionId { get; set; }
    public string Status { get; set; }
    
    public string TransactionStatus { get; set; }

    public string Message { get; set; }

    public long BlockHeight { get; set; }
    public int RetryCount { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }
}