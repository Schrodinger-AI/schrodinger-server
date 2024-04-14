using Orleans;

namespace SchrodingerServer.Grains.Grain.ContractInvoke;

public interface IContractInvokeGrain : IGrainWithStringKey
{
    Task<GrainResultDto<ContractInvokeGrainDto>> CreateAsync(ContractInvokeGrainDto input);

    Task<GrainResultDto<ContractInvokeGrainDto>> ExecuteJobAsync(ContractInvokeGrainDto input);
}