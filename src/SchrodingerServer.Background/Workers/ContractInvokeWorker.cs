using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchrodingerServer.Background.Services;
using SchrodingerServer.Common.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace SchrodingerServer.Background.Workers;

public class ContractInvokeWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IContractInvokeService _contractInvokeService;
    private readonly IOptionsMonitor<ContractSyncOptions> _contractSyncOptionsMonitor;

    public ContractInvokeWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IContractInvokeService contractInvokeService,
        IOptionsMonitor<ContractSyncOptions> contractSyncOptionsMonitor) :
        base(timer, serviceScopeFactory)
    {
        _contractInvokeService = contractInvokeService;
        _contractSyncOptionsMonitor = contractSyncOptionsMonitor;
        Timer.Period = 1000 * contractSyncOptionsMonitor.CurrentValue.Sync;
        contractSyncOptionsMonitor.OnChange((_, _) =>
        {
            Timer.Period = 1000 * contractSyncOptionsMonitor.CurrentValue.Sync;
        });
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        Logger.LogInformation("Executing contract invoke job");
        var bizIds = await _contractInvokeService.SearchUnfinishedTransactionsAsync(_contractSyncOptionsMonitor
            .CurrentValue.Limit);
        var tasks = new List<Task>();
        foreach (var bizId in bizIds)
        {
            tasks.Add(Task.Run(() => { _contractInvokeService.ExecuteJobAsync(bizId); }));
        }

        await Task.WhenAll(tasks);
    }
}