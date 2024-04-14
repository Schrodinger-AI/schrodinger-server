using Orleans;

namespace SchrodingerServer.Grains.Grain.Faucets;

public interface IFaucetsGrain : IGrainWithStringKey
{
     Task<GrainResultDto<FaucetsGrainDto>> FaucetsTransfer(FaucetsTransferGrainDto grain);
}