using Hangfire;
using Microsoft.Extensions.Options;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Options;
using SchrodingerServer.Symbol;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Background.Services;

public interface IInitJobsService
{
    void InitRecurringJob();
}

public class InitJobsService : IInitJobsService, ISingletonDependency
{
    private readonly IRecurringJobManager _recurringJobs;
    private readonly UpdateScoreOptions _options;

    public InitJobsService(IRecurringJobManager recurringJobs, IOptionsSnapshot<UpdateScoreOptions> options)
    {
        _recurringJobs = recurringJobs;
        _options = options.Value;
    }
    
    public void InitRecurringJob()
    {
        _recurringJobs.AddOrUpdate<IZealyScoreService>("IZealyScoreService",
            x => x.UpdateScoreAsync(), _options.RecurringCorn);
        _recurringJobs.AddOrUpdate<IXgrPriceService>("IXgrPriceService",
            x => x.SaveXgrDayPriceAsync(false), _options.PriceRecurringCorn);
        _recurringJobs.AddOrUpdate<IXgrPriceService>("IXgrPriceService",
            x => x.SaveUniqueXgrDayPriceAsync(true), _options.GatePriceRecurringCorn);
    }
}