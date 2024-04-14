using SchrodingerServer.Grains.Grain.ContractInvoke;

namespace SchrodingerServer.Grains.State.ContractInvoke;

public class ContractInvokeState : ContractInvokeGrainDto
{
    public long RefBlockNumber { get; set; }
}