using Orleans;
using SchrodingerServer.Grains.State.Sync;

namespace SchrodingerServer.Grains.Grain.Synchronize;

public interface ISubscribeGrain : IGrainWithStringKey
{
    Task<long> GetSubscribeHeightAsync();
    Task SetSubscribeHeightAsync(long subscribeHeight);
}

public class SubscribeGrain : Grain<SubscribeState>, ISubscribeGrain
{
    public Task<long> GetSubscribeHeightAsync() => Task.FromResult(State.SubscribeHeight);

    public async Task SetSubscribeHeightAsync(long subscribeHeight)
    {
        State.SubscribeHeight = subscribeHeight;
        await WriteStateAsync();
    }
}