using System;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.CosmosDB;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Orleans;
using Orleans.Configuration;
using Orleans.Providers.MongoDB.Configuration;
using SchrodingerServer.Background.Services;
using SchrodingerServer.Background.Workers;
using SchrodingerServer.Common;
using SchrodingerServer.Grains;
using SchrodingerServer.MongoDB;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.BackgroundJobs.Hangfire;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Modularity;
using Volo.Abp.Threading;
using Polly;
using SchrodingerServer.CoinGeckoApi;
using SchrodingerServer.Common.GraphQL;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Options;
using SchrodingerServer.Points;
using SchrodingerServer.Token;
using StackExchange.Redis;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;

namespace SchrodingerServer.Background;

[DependsOn(
    typeof(SchrodingerServerApplicationModule),
    typeof(SchrodingerServerApplicationContractsModule),
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(AbpBackgroundWorkersModule),
    typeof(AbpAutofacModule),
    typeof(SchrodingerServerGrainsModule),
    typeof(AbpEventBusRabbitMqModule),
    typeof(SchrodingerServerDomainModule),
    typeof(SchrodingerServerMongoDbModule),
    typeof(AbpBackgroundJobsHangfireModule)
)]
public class SchrodingerServerBackgroundModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<SchrodingerServerBackgroundModule>(); });

        var configuration = context.Services.GetConfiguration();
        Configure<PointTradeOptions>(configuration.GetSection("PointTradeOptions"));
        Configure<ZealyUserOptions>(configuration.GetSection("ZealyUser"));
        Configure<UpdateScoreOptions>(configuration.GetSection("UpdateScore"));
        Configure<ZealyScoreOptions>(configuration.GetSection("ZealyScore"));
        Configure<ContractSyncOptions>(configuration.GetSection("Sync"));
        Configure<CoinGeckoOptions>(configuration.GetSection("CoinGecko"));
        Configure<CmsConfigOptions>(configuration.GetSection("CmsConfig"));
        Configure<PointContractOptions>(configuration.GetSection("PointContract"));
        Configure<ExchangeOptions>(configuration.GetSection("Exchange"));
        
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        ConfigureRedis(context, configuration, hostingEnvironment);
        ConfigureGraphQl(context, configuration);
        ConfigureCache(configuration);
        context.Services.AddHostedService<SchrodingerServerHostService>();
        context.Services.AddSingleton<IPointSettleService, PointSettleService>();
        context.Services.AddTransient<IExchangeProvider, GateIoProvider>();
        context.Services.AddHttpClient();
        ConfigureHangfire(context, configuration);
        ConfigureZealyClient(context, configuration);
        ConfigureOrleans(context, configuration);
    }
    
    private void ConfigureCache(IConfiguration configuration)
    {
        Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = "SchrodingerServer:"; });
    }

  
    private static void ConfigureOrleans(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddSingleton(o =>
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
                .ConfigureApplicationParts(parts =>
                    parts.AddApplicationPart(typeof(SchrodingerServerGrainsModule).Assembly).WithReferences())
                .ConfigureLogging(builder => builder.AddProvider(o.GetService<ILoggerProvider>()))
                .Build();
        });
    }

    private void ConfigureZealyClient(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddHttpClient(CommonConstant.ZealyClientName, httpClient =>
        {
            httpClient.BaseAddress = new Uri(configuration["Zealy:BaseUrl"]);
            httpClient.DefaultRequestHeaders.Add(
                CommonConstant.ZealyApiKeyName, configuration["Zealy:ApiKey"]);
        }).AddTransientHttpErrorPolicy(policyBuilder =>
            policyBuilder.WaitAndRetryAsync(
                3, retryNumber => TimeSpan.FromMilliseconds(50)));
        ;
    }

    private void ConfigureHangfire(ServiceConfigurationContext context, IConfiguration configuration)
    {
        var mongoType = configuration["Hangfire:MongoType"];
        var connectionString = configuration["Hangfire:ConnectionString"];
        if (connectionString.IsNullOrEmpty()) return;

        if (mongoType.IsNullOrEmpty() ||
            mongoType.Equals(MongoType.MongoDb.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            context.Services.AddHangfire(x =>
            {
                x.UseMongoStorage(connectionString, new MongoStorageOptions
                {
                    MigrationOptions = new MongoMigrationOptions
                    {
                        MigrationStrategy = new MigrateMongoMigrationStrategy(),
                        BackupStrategy = new CollectionMongoBackupStrategy()
                    },
                    CheckConnection = true,
                    CheckQueuedJobsStrategy = CheckQueuedJobsStrategy.TailNotificationsCollection
                });
            });
        }
        else if (mongoType.Equals(MongoType.DocumentDb.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            context.Services.AddHangfire(config =>
            {
                var mongoUrlBuilder = new MongoUrlBuilder(connectionString);
                var mongoClient = new MongoClient(mongoUrlBuilder.ToMongoUrl());
                var opt = new CosmosStorageOptions
                {
                    MigrationOptions = new MongoMigrationOptions
                    {
                        BackupStrategy = new NoneMongoBackupStrategy(),
                        MigrationStrategy = new DropMongoMigrationStrategy(),
                    }
                };
                config.UseCosmosStorage(mongoClient, mongoUrlBuilder.DatabaseName, opt);
            });
        }

        context.Services.AddHangfireServer(opt => { opt.Queues = new[] { "background" }; });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        context.AddBackgroundWorkerAsync<UserRelationWorker>();
        context.AddBackgroundWorkerAsync<ContractInvokeWorker>();
        context.AddBackgroundWorkerAsync<UniswapPriceSnapshotWorker>();
        context.AddBackgroundWorkerAsync<XpScoreSettleWorker>();
        context.AddBackgroundWorkerAsync<XpScoreResultWorker>();
        InitRecurringJob(context.ServiceProvider);
        StartOrleans(context.ServiceProvider);
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        StopOrleans(context.ServiceProvider);
    }

    private static void InitRecurringJob(IServiceProvider serviceProvider)
    {
        var jobsService = serviceProvider.GetRequiredService<IInitJobsService>();
        jobsService.InitRecurringJob();
    }

    private static void StartOrleans(IServiceProvider serviceProvider)
    {
        var client = serviceProvider.GetRequiredService<IClusterClient>();
        AsyncHelper.RunSync(async () => await client.Connect());
    }

    private static void StopOrleans(IServiceProvider serviceProvider)
    {
        var client = serviceProvider.GetRequiredService<IClusterClient>();
        AsyncHelper.RunSync(client.Close);
    }
    
    private void ConfigureRedis(
        ServiceConfigurationContext context,
        IConfiguration configuration,
        IWebHostEnvironment hostingEnvironment)
    {
        if (!hostingEnvironment.IsDevelopment())
        {
            var redis = ConnectionMultiplexer.Connect(configuration["Redis:Configuration"]);
            context.Services
                .AddDataProtection()
                .PersistKeysToStackExchangeRedis(redis, "SchrodingerServer-Protection-Keys");
        }
    }
    
    private void ConfigureGraphQl(ServiceConfigurationContext context,
        IConfiguration configuration)
    {
        context.Services.AddSingleton(new GraphQLHttpClient(configuration["GraphQL:Configuration"],
            new NewtonsoftJsonSerializer()));
        context.Services.AddScoped<IGraphQLClient>(sp => sp.GetRequiredService<GraphQLHttpClient>());
        Configure<GraphQLOptions>(configuration.GetSection("GraphQL"));
    }
}