using Orleans;
using SchrodingerServer.Grains.State.Sync;

namespace SchrodingerServer.Grains.Grain.Synchronize;

public interface ISyncPendingGrain : IGrainWithStringKey
{
    Task<List<string>> GetSyncPendingListAsync();
    Task AddOrUpdateSyncPendingList(List<string> transactions);
    Task DeleteSyncPendingList(List<string> deletePending);
}

public class SyncPendingGrain : Grain<SyncPendingState>, ISyncPendingGrain
{
    public async Task<List<string>> GetSyncPendingListAsync() => State.SyncPendingList;

    public async Task AddOrUpdateSyncPendingList(List<string> transactions)
    {
        if (State.SyncPendingList == null)
        {
            State.SyncPendingList = transactions;
        }
        else
        {
            var set = new HashSet<string>(transactions);
            set.UnionWith(State.SyncPendingList);
            State.SyncPendingList = set.ToList();
        }

        await WriteStateAsync();
    }

    public async Task DeleteSyncPendingList(List<string> deletePending)
    {
        State.SyncPendingList.RemoveAll(deletePending.Contains);

        await WriteStateAsync();
    }
}