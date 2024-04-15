using AElf.Client.Service;
using Microsoft.Extensions.DependencyInjection;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Grains;
using SchrodingerServer.Grains.Grain.ApplicationHandler;
using SchrodingerServer.Grains.Grain.Provider;
using SchrodingerServer.MongoDB;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;
namespace SchrodingerServer.Silo;

[DependsOn(
    typeof(SchrodingerServerGrainsModule),
    typeof(AbpAspNetCoreMvcUiMultiTenancyModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(SchrodingerServerApplicationModule),
    typeof(SchrodingerServerMongoDbModule),
    typeof(AbpAutofacModule)
)]
public class SchrodingerServerOrleansSiloModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<SchrodingerServerOrleansSiloModule>(); });
        context.Services.AddHostedService<SchrodingerServerHostedService>();
        var configuration = context.Services.GetConfiguration();
        Configure<ChainOptions>(configuration.GetSection("Chains"));
        Configure<FaucetsTransferOptions>(configuration.GetSection("Faucets"));
        Configure<SyncTokenOptions>(configuration.GetSection("Sync"));
        context.Services.AddSingleton<IContractProvider, ContractProvider>();
        Configure<SecurityServerOptions>(configuration.GetSection("SecurityServer"));
        context.Services.AddSingleton<IBlockchainClientFactory<AElfClient>, AElfClientFactory>();

        context.Services.AddHttpClient();
    }
}