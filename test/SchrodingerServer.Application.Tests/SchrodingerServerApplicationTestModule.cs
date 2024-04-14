using Microsoft.Extensions.DependencyInjection;
using Moq;
using SchrodingerServer.EntityEventHandler.Core;
using Volo.Abp.AuditLogging;
using Volo.Abp.AuditLogging.MongoDB;
using Volo.Abp.AutoMapper;
using Volo.Abp.EventBus;
using Volo.Abp.Identity;
using Volo.Abp.Identity.MongoDB;
using Volo.Abp.Modularity;
using Volo.Abp.MongoDB;

namespace SchrodingerServer;

[DependsOn(
    typeof(AbpEventBusModule),
    typeof(SchrodingerServerApplicationModule),
    typeof(SchrodingerServerApplicationContractsModule),
    typeof(SchrodingerServerOrleansTestBaseModule),
    typeof(SchrodingerServerDomainTestModule)
)]
public class SchrodingerServerApplicationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        base.ConfigureServices(context);
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<SchrodingerServerApplicationModule>(); });
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<SchrodingerServerEntityEventHandlerCoreModule>(); });

        context.Services.AddSingleton(new Mock<IMongoDbContextProvider<IAuditLoggingMongoDbContext>>().Object);
        context.Services.AddSingleton<IAuditLogRepository, MongoAuditLogRepository>();
        context.Services.AddSingleton<IIdentityUserRepository, MongoIdentityUserRepository>();
    }
}