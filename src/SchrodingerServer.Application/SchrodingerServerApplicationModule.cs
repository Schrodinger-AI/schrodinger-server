using Microsoft.Extensions.DependencyInjection;
using SchrodingerServer.Cat;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.GateIo;
using SchrodingerServer.Grains;
using SchrodingerServer.Options;
using Volo.Abp.Account;
using Volo.Abp.AutoMapper;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.TenantManagement;

namespace SchrodingerServer;

[DependsOn(
    typeof(SchrodingerServerDomainModule),
    typeof(AbpAccountApplicationModule),
    typeof(SchrodingerServerApplicationContractsModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpTenantManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(SchrodingerServerGrainsModule),
    typeof(AbpSettingManagementApplicationModule)
)]
public class SchrodingerServerApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<SchrodingerServerApplicationModule>(); });
        context.Services.AddSingleton(typeof(ILocalMemoryCache<>), typeof(LocalMemoryCache<>));
        context.Services.AddHttpClient();
        context.Services.AddSingleton<IGateIoCirculationService, GateIoCirculationService>();

        var configuration = context.Services.GetConfiguration();
        Configure<CmsConfigOptions>(configuration.GetSection("CmsConfig"));
        Configure<UniswapV3Options>(configuration.GetSection("UniswapV3"));
        Configure<SgrCirculationOptions>(configuration.GetSection("SgrCirculation"));
        
        context.Services.AddSingleton<ISchrodingerCatService, SchrodingerCatService>();
    }
    
}
