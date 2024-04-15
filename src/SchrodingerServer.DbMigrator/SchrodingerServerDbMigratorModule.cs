using SchrodingerServer.MongoDB;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Modularity;

namespace SchrodingerServer.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(SchrodingerServerMongoDbModule),
    typeof(SchrodingerServerApplicationContractsModule)
    )]
public class SchrodingerServerDbMigratorModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpBackgroundJobOptions>(options => options.IsJobExecutionEnabled = false);
    }
}
