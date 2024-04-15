using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchrodingerServer.Background.Services;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace SchrodingerServer.Background.Workers;

public class XpScoreSettleWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly ILogger<XpScoreSettleWorker> _logger;
    private readonly IXpScoreSettleService _scoreSettleService;
    private readonly UpdateScoreOptions _options;

    public XpScoreSettleWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IOptionsSnapshot<UpdateScoreOptions> options, ILogger<XpScoreSettleWorker> logger,
        IXpScoreSettleService scoreSettleService) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _scoreSettleService = scoreSettleService;
        _options = options.Value;
        timer.Period = options.Value.SettleXpScorePeriod * 60 * 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        if (!_options.OpenXpScoreSettle)
        {
            _logger.LogInformation("settle xp record not open.");
            return;
        }
        _logger.LogInformation("begin execute XpScoreSettleWorker.");
        await _scoreSettleService.BatchSettleAsync();
        _logger.LogInformation("finish execute XpScoreSettleWorker.");
    }
}