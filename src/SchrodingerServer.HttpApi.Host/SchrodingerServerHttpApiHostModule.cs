using System;
using System.IO;
using System.Linq;
using AutoResponseWrapper;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Orleans;
using Orleans.Configuration;
using Orleans.Providers.MongoDB.Configuration;
using SchrodingerServer.CoinGeckoApi;
using SchrodingerServer.Common.GraphQL;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Faucets;
using StackExchange.Redis;
using SchrodingerServer.Grains;
using SchrodingerServer.Middleware;
using SchrodingerServer.MongoDB;
using SchrodingerServer.Options;
using SchrodingerServer.Users;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.BlobStoring.Aliyun;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.Swashbuckle;
using Volo.Abp.Threading;
using Volo.Abp.VirtualFileSystem;

namespace SchrodingerServer
{
    [DependsOn(
        typeof(SchrodingerServerHttpApiModule),
        typeof(AbpAutofacModule),
        typeof(AbpCachingStackExchangeRedisModule),
        typeof(AbpAspNetCoreMvcUiMultiTenancyModule),
        typeof(SchrodingerServerApplicationModule),
        typeof(SchrodingerServerMongoDbModule),
        typeof(AbpAspNetCoreSerilogModule),
        typeof(AbpSwashbuckleModule),
        typeof(AbpEventBusRabbitMqModule),
        typeof(AbpBlobStoringAliyunModule)
    )]
    public class SchrodingerServerHttpApiHostModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var configuration = context.Services.GetConfiguration();
            var hostingEnvironment = context.Services.GetHostingEnvironment();
            Configure<ChainOptions>(configuration.GetSection("Chains"));
            Configure<TraitsOptions>(configuration.GetSection("Traits"));
            Configure<AdoptImageOptions>(configuration.GetSection("AdoptImageOptions"));
            Configure<TransactionFeeOptions>(configuration.GetSection("TransactionFeeInfo"));
            Configure<CoinGeckoOptions>(configuration.GetSection("CoinGecko"));
            Configure<SecurityServerOptions>(configuration.GetSection("SecurityServer"));
            Configure<FaucetsOptions>(configuration.GetSection("Faucets"));
            Configure<CmsConfigOptions>(configuration.GetSection("CmsConfig"));
            Configure<IpfsOptions>(configuration.GetSection("Ipfs"));
            Configure<AwsS3Option>(configuration.GetSection("AwsS3"));
            Configure<StableDiffusionOption>(configuration.GetSection("StableDiffusionOption"));
            Configure<LevelOptions>(configuration.GetSection("LevelOptions"));
            Configure<PointTradeOptions>(configuration.GetSection("PointTradeOptions"));
            Configure<ActivityOptions>(configuration.GetSection("ActivityOptions"));
            Configure<ActivityRankOptions>(configuration.GetSection("ActivityRankOptions"));
            Configure<ActivityTraitOptions>(configuration.GetSection("ActivityTraitOptions"));
            Configure<TasksOptions>(configuration.GetSection("TasksOptions"));
            Configure<SpinRewardOptions>(configuration.GetSection("SpinRewardOptions"));
            Configure<PoolOptions>(configuration.GetSection("PoolOptions"));

            ConfigureConventionalControllers();
            ConfigureAuthentication(context, configuration);
            ConfigureLocalization();
            ConfigureCache(configuration);
            ConfigureVirtualFileSystem(context);
            ConfigureRedis(context, configuration, hostingEnvironment);
            ConfigureCors(context, configuration);
            ConfigureSwaggerServices(context, configuration);
            // ConfigureOrleans(context, configuration);
            ConfigureGraphQl(context, configuration);
            context.Services.AddAutoResponseWrapper();
            context.Services.AddSingleton<ILevelProvider, LevelProvider>();

        }

        private void ConfigureCache(IConfiguration configuration)
        {
            Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = "SchrodingerServer:"; });
        }

        private void ConfigureAuthentication(ServiceConfigurationContext context, IConfiguration configuration)
        {
            context.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = configuration["AuthServer:Authority"];
                    options.RequireHttpsMetadata = Convert.ToBoolean(configuration["AuthServer:RequireHttpsMetadata"]);
                    options.Audience = "SchrodingerServer";
                });
        }


        private void ConfigureVirtualFileSystem(ServiceConfigurationContext context)
        {
            var hostingEnvironment = context.Services.GetHostingEnvironment();

            if (hostingEnvironment.IsDevelopment())
            {
                Configure<AbpVirtualFileSystemOptions>(options =>
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<SchrodingerServerDomainSharedModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}SchrodingerServer.Domain.Shared"));
                    options.FileSets.ReplaceEmbeddedByPhysical<SchrodingerServerDomainModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}SchrodingerServer.Domain"));
                    options.FileSets.ReplaceEmbeddedByPhysical<SchrodingerServerApplicationContractsModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}SchrodingerServer.Application.Contracts"));
                    options.FileSets.ReplaceEmbeddedByPhysical<SchrodingerServerApplicationModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}SchrodingerServer.Application"));
                });
            }
        }

        private void ConfigureConventionalControllers()
        {
            Configure<AbpAspNetCoreMvcOptions>(options =>
            {
                options.ConventionalControllers.Create(typeof(SchrodingerServerHttpApiModule).Assembly);
            });
        }


        private static void ConfigureSwaggerServices(ServiceConfigurationContext context, IConfiguration configuration)
        {
            context.Services.AddAbpSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "SchrodingerServer API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
                {
                    Name = "Authorization",
                    Scheme = "bearer",
                    Description = "Specify the authorization token.",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        new string[] { }
                    }
                });
            });
        }

        private void ConfigureLocalization()
        {
            Configure<AbpLocalizationOptions>(options =>
            {
                options.Languages.Add(new LanguageInfo("ar", "ar", "العربية"));
                options.Languages.Add(new LanguageInfo("cs", "cs", "Čeština"));
                options.Languages.Add(new LanguageInfo("en", "en", "English"));
                options.Languages.Add(new LanguageInfo("en-GB", "en-GB", "English (UK)"));
                options.Languages.Add(new LanguageInfo("fi", "fi", "Finnish"));
                options.Languages.Add(new LanguageInfo("fr", "fr", "Français"));
                options.Languages.Add(new LanguageInfo("hi", "hi", "Hindi"));
                options.Languages.Add(new LanguageInfo("it", "it", "Italian"));
                options.Languages.Add(new LanguageInfo("hu", "hu", "Magyar"));
                options.Languages.Add(new LanguageInfo("pt-BR", "pt-BR", "Português"));
                options.Languages.Add(new LanguageInfo("ru", "ru", "Русский"));
                options.Languages.Add(new LanguageInfo("sk", "sk", "Slovak"));
                options.Languages.Add(new LanguageInfo("tr", "tr", "Türkçe"));
                options.Languages.Add(new LanguageInfo("zh-Hans", "zh-Hans", "简体中文"));
                options.Languages.Add(new LanguageInfo("zh-Hant", "zh-Hant", "繁體中文"));
                options.Languages.Add(new LanguageInfo("de-DE", "de-DE", "Deutsch"));
                options.Languages.Add(new LanguageInfo("es", "es", "Español"));
            });
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

        private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
        {
            context.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder
                        .WithOrigins(
                            configuration["App:CorsOrigins"]
                                .Split(",", StringSplitOptions.RemoveEmptyEntries)
                                .Select(o => o.RemovePostFix("/"))
                                .ToArray()
                        )
                        .WithAbpExposedHeaders()
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
        }

        // private static void ConfigureOrleans(ServiceConfigurationContext context, IConfiguration configuration)
        // {
        //     context.Services.AddSingleton<IClusterClient>(o =>
        //     {
        //         return new ClientBuilder()
        //             .ConfigureDefaults()
        //             .UseMongoDBClient(configuration["Orleans:MongoDBClient"])
        //             .UseMongoDBClustering(options =>
        //             {
        //                 options.DatabaseName = configuration["Orleans:DataBase"];
        //                 options.Strategy = MongoDBMembershipStrategy.SingleDocument;
        //             })
        //             .Configure<ClusterOptions>(options =>
        //             {
        //                 options.ClusterId = configuration["Orleans:ClusterId"];
        //                 options.ServiceId = configuration["Orleans:ServiceId"];
        //             })
        //             .ConfigureApplicationParts(parts =>
        //                 parts.AddApplicationPart(typeof(SchrodingerServerGrainsModule).Assembly).WithReferences())
        //             .ConfigureLogging(builder => builder.AddProvider(o.GetService<ILoggerProvider>()))
        //             .Build();
        //     });
        // }

        private void ConfigureGraphQl(ServiceConfigurationContext context,
            IConfiguration configuration)
        {
            context.Services.AddSingleton(new GraphQLHttpClient(configuration["GraphQL:Configuration"],
                new NewtonsoftJsonSerializer()));
            context.Services.AddScoped<IGraphQLClient>(sp => sp.GetRequiredService<GraphQLHttpClient>());
            Configure<GraphQLOptions>(configuration.GetSection("GraphQL"));
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var app = context.GetApplicationBuilder();
            var env = context.GetEnvironment();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseCorrelationId();

            app.UseMiddleware<DeviceInfoMiddleware>();

            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors();
            app.UseAuthentication();

            app.UseAbpRequestLocalization();
            app.UseAuthorization();

            // if (env.IsDevelopment())
            // {
            app.UseSwagger();
            app.UseAbpSwaggerUI(options => { options.SwaggerEndpoint("/swagger/v1/swagger.json", "Support APP API"); });
            // }

            app.UseAuditing();
            app.UseAbpSerilogEnrichers();
            app.UseUnitOfWork();
            app.UseConfiguredEndpoints();

            // StartOrleans(context.ServiceProvider);
        }

        // public override void OnApplicationShutdown(ApplicationShutdownContext context)
        // {
        //     StopOrleans(context.ServiceProvider);
        // }
        //
        // private static void StartOrleans(IServiceProvider serviceProvider)
        // {
        //     var client = serviceProvider.GetRequiredService<IClusterClient>();
        //     AsyncHelper.RunSync(async () => await client.Connect());
        // }
        //
        // private static void StopOrleans(IServiceProvider serviceProvider)
        // {
        //     var client = serviceProvider.GetRequiredService<IClusterClient>();
        //     AsyncHelper.RunSync(client.Close);
        // }
    }
}