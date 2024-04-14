using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace SchrodingerServer.Worker.Core;

[DependsOn(
    typeof(AbpAutoMapperModule)
)]
public class SchrodingerServerWorkerCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<SchrodingerServerWorkerCoreModule>(); });
    }
}