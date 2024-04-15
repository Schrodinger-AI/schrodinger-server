using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchrodingerServer.Common.Options;
using SchrodingerServer.EntityEventHandler.Core.IndexHandler;
using SchrodingerServer.EntityEventHandler.Core.Options;
using StackExchange.Redis;
using Volo.Abp.AutoMapper;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Modularity;

namespace SchrodingerServer.EntityEventHandler.Core
{
    [DependsOn(typeof(AbpAutoMapperModule),
        typeof(AbpDistributedLockingModule),
        typeof(SchrodingerServerApplicationModule),
        typeof(SchrodingerServerApplicationContractsModule))]
    public class SchrodingerServerEntityEventHandlerCoreModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<AbpAutoMapperOptions>(options => { options.AddMaps<SchrodingerServerEntityEventHandlerCoreModule>(); });
            var configuration = context.Services.GetConfiguration();
            ConfigureRateLimiting(context, configuration);
            ConfigureDistributedLocking(context, configuration);
            context.Services.AddSingleton<IRateDistributeLimiter, RateDistributeLimiter>();
        }

        private void ConfigureRateLimiting(ServiceConfigurationContext context, IConfiguration configuration)
        {
            var multiplexer = ConnectionMultiplexer.Connect(configuration["Redis:Configuration"]);
            context.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

            Configure<RateLimitOptions>(configuration.GetSection("RateLimitOptions"));
            Configure<StableDiffusionOption>(configuration.GetSection("StableDiffusionOption"));
            Configure<TraitsOptions>(configuration.GetSection("Traits"));
        }
        
        private void ConfigureDistributedLocking(ServiceConfigurationContext context, IConfiguration configuration)
        {
            context.Services.AddSingleton<IDistributedLockProvider>(sp =>
            {
                var connection = ConnectionMultiplexer.Connect(configuration["Redis:Configuration"]);
                return new RedisDistributedSynchronizationProvider(connection.GetDatabase());
            });
        }
    }
}