using Microsoft.Extensions.Logging;
using Orleans;
using SchrodingerServer.Grains.Grain.Provider;
using SchrodingerServer.Grains.State.Sync;

namespace SchrodingerServer.Grains.Grain.Synchronize;

public interface IIndexBlockHeightGrain : IGrainWithStringKey
{
    Task<long> UpdateSideChainIndexHeightAsync(string targetChainId, string sourceChainId);
    Task<long> GetSideChainIndexHeightAsync();
    Task<long> UpdateMainChainIndexHeightAsync(string sourceChainId);
    Task<long> GetMainChainIndexHeightAsync();
}

public class IndexBlockHeightGrain : Grain<IndexBlockHeightGrainState>, IIndexBlockHeightGrain
{
    private readonly IContractProvider _contractProvider;
    private readonly ILogger<IndexBlockHeightGrain> _logger;

    public IndexBlockHeightGrain(IContractProvider contractProvider, ILogger<IndexBlockHeightGrain> logger)
    {
        _logger = logger;
        _contractProvider = contractProvider;
    }

    public async Task<long> UpdateSideChainIndexHeightAsync(string targetChainId, string sourceChainId)
    {
        State.SideChainIndexHeight = await _contractProvider.GetSideChainIndexHeightAsync(targetChainId, sourceChainId);
        await WriteStateAsync();

        _logger.LogInformation("Updated side chain index height to {height}", State.SideChainIndexHeight);

        return State.SideChainIndexHeight;
    }


    public async Task<long> UpdateMainChainIndexHeightAsync(string sourceChainId)
    {
        State.MainChainIndexHeight = await _contractProvider.GetIndexHeightAsync(sourceChainId);
        await WriteStateAsync();

        _logger.LogInformation("Updated main chain index height to {height}", State.MainChainIndexHeight);

        return State.MainChainIndexHeight;
    }

    public Task<long> GetSideChainIndexHeightAsync() => Task.FromResult(State.SideChainIndexHeight);
    public Task<long> GetMainChainIndexHeightAsync() => Task.FromResult(State.MainChainIndexHeight);
}