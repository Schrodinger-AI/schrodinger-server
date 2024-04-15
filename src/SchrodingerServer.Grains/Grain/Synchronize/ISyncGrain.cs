using Orleans;

namespace SchrodingerServer.Grains.Grain.Synchronize;

public interface ISyncGrain : IGrainWithStringKey
{
    Task<GrainResultDto<SyncGrainDto>> ExecuteJobAsync(SyncJobGrainDto input);
}