using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchrodingerServer.Background.Services;
using SchrodingerServer.Common.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace SchrodingerServer.Background.Workers;

public class XpScoreResultWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IXpScoreResultService _xpScoreResultService;
    private readonly ILogger<XpScoreResultService> _logger;
    private readonly UpdateScoreOptions _options;

    public XpScoreResultWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IXpScoreResultService xpScoreResultService, ILogger<XpScoreResultService> logger,
        IOptionsSnapshot<UpdateScoreOptions> options) : base(timer,
        serviceScopeFactory)
    {
        _xpScoreResultService = xpScoreResultService;
        _logger = logger;
        _options = options.Value;
        timer.Period = options.Value.UpdateXpScoreResultPeriod * 60 * 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        if (!_options.OpenXpScoreResult)
        {
            _logger.LogInformation("set xp record result not open.");
            return;
        }
        
        _logger.LogInformation("XpScoreResultWorker begin");
        await _xpScoreResultService.HandleXpResultAsync();
        _logger.LogInformation("XpScoreResultWorker finish");
    }
}