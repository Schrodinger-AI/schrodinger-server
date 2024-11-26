
namespace SchrodingerServer.Grains.Grain.ContractInvoke;

[GenerateSerializer]
public class ContractInvokeGrainDto
{
    [Id(0)]
    public string Id { get; set; }

    [Id(1)]
    public string ChainId { get; set; }

    [Id(2)]
    public string BizId { get; set; }

    [Id(3)]
    public string BizType { get; set; }
    [Id(4)]
    public string ContractAddress { get; set; }
    [Id(5)]
    public string ContractMethod { get; set; }
    [Id(6)]
    public string Sender { get; set; }

    [Id(7)]
    public string ParamJson { get; set; }

    [Id(8)]
    public string Param { get; set; }
    [Id(9)]
    public string TransactionId { get; set; }
    [Id(10)]
    public string Status { get; set; }
    [Id(11)]
    public string TransactionStatus { get; set; }

    [Id(12)]
    public string Message { get; set; }

    [Id(13)]
    public long BlockHeight { get; set; }
    [Id(14)]
    public int RetryCount { get; set; }

    [Id(15)]
    public DateTime CreateTime { get; set; }

    [Id(16)]
    public DateTime UpdateTime { get; set; }
}