using Microsoft.Extensions.DependencyInjection;
using SchrodingerServer.EntityEventHandler.Core;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Modularity;

namespace SchrodingerServer;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpAuthorizationModule),
    typeof(SchrodingerServerDomainModule)
)]
public class SchrodingerServerTestBaseModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpBackgroundJobOptions>(options => { options.IsJobExecutionEnabled = false; });

        context.Services.AddAlwaysAllowAuthorization();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
    }
}