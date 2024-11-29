using SchrodingerServer.Grains.Grain.ContractInvoke;

namespace SchrodingerServer.Grains.State.ContractInvoke;

[GenerateSerializer]
public class ContractInvokeState : ContractInvokeGrainDto
{
    [Id(0)]
    public long RefBlockNumber { get; set; }
}