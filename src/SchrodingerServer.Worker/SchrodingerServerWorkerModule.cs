using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Providers.MongoDB.Configuration;
using SchrodingerServer.Grains;
using SchrodingerServer.Worker.Core;
using SchrodingerServer.Worker.Core.Options;
using SchrodingerServer.Worker.Core.Worker;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Modularity;
using Volo.Abp.Threading;

namespace SchrodingerServer.Worker;

[DependsOn(
    typeof(SchrodingerServerWorkerCoreModule),
    typeof(AbpAspNetCoreMvcUiMultiTenancyModule),
    typeof(AbpBackgroundWorkersModule),
    typeof(AbpAutoMapperModule),
    typeof(AbpAutofacModule)
)]
public class SchrodingerServerWorkerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        Configure<WorkerOptions>(configuration.GetSection("Worker"));
        Configure<IndexBlockHeightOptions>(configuration.GetSection("IndexBlockHeight"));
        context.Services.AddHttpClient();

        ConfigureGraphQl(context, configuration);
        ConfigureOrleans(context, configuration);
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        StartOrleans(context.ServiceProvider);
        context.AddBackgroundWorkerAsync<SyncWorker>();
        context.AddBackgroundWorkerAsync<IndexBlockHeightWorker>();
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        StopOrleans(context.ServiceProvider);
    }

    private void ConfigureGraphQl(ServiceConfigurationContext context, IConfiguration configuration)
    {
        Configure<GraphqlOptions>(configuration.GetSection("GraphQL"));
        context.Services.AddSingleton(new GraphQLHttpClient(configuration["GraphQL:Configuration"],
            new NewtonsoftJsonSerializer()));
        context.Services.AddScoped<IGraphQLClient>(sp => sp.GetRequiredService<GraphQLHttpClient>());
    }

    private static void ConfigureOrleans(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddSingleton<IClusterClient>(o =>
        {
            return new ClientBuilder()
                .ConfigureDefaults()
                .UseMongoDBClient(configuration["Orleans:MongoDBClient"])
                .UseMongoDBClustering(options =>
                {
                    options.DatabaseName = configuration["Orleans:DataBase"];
                    options.Strategy = MongoDBMembershipStrategy.SingleDocument;
                })
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = configuration["Orleans:ClusterId"];
                    options.ServiceId = configuration["Orleans:ServiceId"];
                })
                .Configure<MessagingOptions>(options => options.ResponseTimeout = TimeSpan.FromSeconds(300))
                .ConfigureApplicationParts(parts =>
                    parts.AddApplicationPart(typeof(SchrodingerServerGrainsModule).Assembly).WithReferences())
                .ConfigureLogging(builder => builder.AddProvider(o.GetService<ILoggerProvider>()))
                .Build();
        });
    }

    private static void StartOrleans(IServiceProvider serviceProvider)
        => AsyncHelper.RunSync(async () => await serviceProvider.GetRequiredService<IClusterClient>().Connect());

    private static void StopOrleans(IServiceProvider serviceProvider)
        => AsyncHelper.RunSync(serviceProvider.GetRequiredService<IClusterClient>().Close);
}