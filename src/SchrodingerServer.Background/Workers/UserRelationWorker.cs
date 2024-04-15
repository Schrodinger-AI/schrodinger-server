using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchrodingerServer.Background.Services;
using SchrodingerServer.Common.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace SchrodingerServer.Background.Workers;

public class UserRelationWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IUserRelationService _userRelationService;
    private readonly ILogger<UserRelationWorker> _logger;

    public UserRelationWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IUserRelationService userRelationService, IOptionsSnapshot<ZealyUserOptions> options,
        ILogger<UserRelationWorker> logger) : base(timer,
        serviceScopeFactory)
    {
        _userRelationService = userRelationService;
        _logger = logger;
        timer.Period = options.Value.Period * 60 * 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogInformation("begin execute UserRelationWorker.");
        await _userRelationService.AddUserRelationAsync();
        _logger.LogInformation("finish execute UserRelationWorker.");
    }
}