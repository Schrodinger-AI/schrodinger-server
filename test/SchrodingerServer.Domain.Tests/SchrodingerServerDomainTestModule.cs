using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AElf.Indexing.Elasticsearch;
using AElf.Indexing.Elasticsearch.Options;
using AElf.Indexing.Elasticsearch.Services;
using Elasticsearch.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SchrodingerServer.Adopts.dispatcher;
using SchrodingerServer.EntityEventHandler.Core.IndexHandler;
using SchrodingerServer.EntityEventHandler.Core.Options;
using SchrodingerServer.Options;
using StackExchange.Redis;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.Threading;

namespace SchrodingerServer;

[DependsOn(
    typeof(SchrodingerServerTestBaseModule)
)]
public class SchrodingerServerDomainTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<IndexCreateOption>(x => { x.AddModule(typeof(SchrodingerServerDomainModule)); });

        // Do not modify this!!!
        context.Services.Configure<EsEndpointOption>(options => { options.Uris = new List<string> { "http://127.0.0.1:9200" }; });
        var multiplexer = ConnectionMultiplexer.Connect("127.0.0.1:6379");
        context.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        context.Services.AddSingleton<IRateDistributeLimiter, RateDistributeLimiter>();
        context.Services.AddSingleton<IImageProvider, AutoMaticImageProvider>();
        context.Services.Configure<RateLimitOptions>(options =>
        {
            options.RedisRateLimitOptions = new List<RateLimitOption>
            {
                new()
                {
                    Name = "test",
                    TokenLimit = 1,
                    TokensPerPeriod = 1,
                    ReplenishmentPeriod = 1
                }
            };
        });
        context.Services.Configure<IndexSettingOptions>(options =>
        {
            options.NumberOfReplicas = 1;
            options.NumberOfShards = 1;
            options.Refresh = Refresh.True;
            options.IndexPrefix = "SchrodingerServertest";
        });
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        var elasticIndexService = context.ServiceProvider.GetRequiredService<IElasticIndexService>();
        var modules = context.ServiceProvider.GetRequiredService<IOptions<IndexCreateOption>>().Value.Modules;

        modules.ForEach(m =>
        {
            var types = GetTypesAssignableFrom<IIndexBuild>(m.Assembly);
            foreach (var t in types)
            {
                AsyncHelper.RunSync(async () =>
                    await elasticIndexService.DeleteIndexAsync("schrodingerservertest." + t.Name.ToLower()));
            }
        });
    }

    private List<Type> GetTypesAssignableFrom<T>(Assembly assembly)
    {
        var compareType = typeof(T);
        return assembly.DefinedTypes
            .Where(type => compareType.IsAssignableFrom(type) && !compareType.IsAssignableFrom(type.BaseType) &&
                           !type.IsAbstract && type.IsClass && compareType != type)
            .Cast<Type>().ToList();
    }
}