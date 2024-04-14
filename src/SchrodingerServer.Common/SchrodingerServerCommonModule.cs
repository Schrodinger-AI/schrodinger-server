using Microsoft.Extensions.DependencyInjection;
using SchrodingerServer.Common.Http;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace SchrodingerServer.Common;

[DependsOn(
    typeof(AbpAutoMapperModule)
)]
public class SchrodingerServerCommonModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<SchrodingerServerCommonModule>(); });
        context.Services.AddSingleton<IHttpProvider, HttpProvider>();
    }
}