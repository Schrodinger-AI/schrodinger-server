using System;
using System.Linq;
using AutoResponseWrapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using SchrodingerServer.SignatureServer.Controllers;
using SchrodingerServer.SignatureServer.Options;
using SchrodingerServer.SignatureServer.Providers;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.Modularity;
using Volo.Abp.Swashbuckle;

namespace SchrodingerServer.SignatureServer
{
    [DependsOn(
        typeof(AbpAutofacModule),
        typeof(AbpCachingStackExchangeRedisModule),
        typeof(AbpAspNetCoreMvcUiMultiTenancyModule),
        typeof(AbpAspNetCoreSerilogModule),
        typeof(AbpAspNetCoreMvcModule),
        typeof(AbpSwashbuckleModule)
    )]
    
    public class SchrodingerServerSignatureApiHostModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var configuration = context.Services.GetConfiguration();
            Configure<KeyPairInfoOptions>(configuration.GetSection("KeyPairInfo"));
            Configure<KeyStoreOptions>(configuration.GetSection("KeyStore"));
            Configure<ThirdPartKeyStoreOptions>(configuration.GetSection("ThirdPartKeyStore"));

            ConfigureConventionalControllers();
            // ConfigureCors(context, configuration);
            // ConfigureSwaggerServices(context, configuration);
            context.Services.AddSingleton<AccountProvider>();
            context.Services.AddSingleton<ISignatureProvider, SignatureProvider>();
            
            context.Services.AddAutoResponseWrapper();
            
            var assembly = typeof(SchrodingerServerSignatureApiHostModule).Assembly;
            var controllerType = assembly.GetTypes().FirstOrDefault(t => t == typeof(SignatureController));
            if (controllerType != null)
            {
                // Controller exists in the assembly
                Console.WriteLine("SignatureController found in the assembly.");
            }
            else
            {
                // Controller does not exist in the assembly
                Console.WriteLine("SignatureController not found in the assembly.");
            }
            
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
            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors();
            app.UseAuthentication();

            app.UseAbpRequestLocalization();
            app.UseAuthorization();

            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseAbpSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Support APP API");
                });
            }

            app.UseAuditing();
            app.UseAbpSerilogEnrichers();
            app.UseConfiguredEndpoints();
            
            _ = context.ServiceProvider.GetService<AccountProvider>();
        }
        
        
        private void ConfigureConventionalControllers()
        {
            Configure<AbpAspNetCoreMvcOptions>(options =>
            {
                options.ConventionalControllers.Create(typeof(SchrodingerServerSignatureApiHostModule).Assembly);
                options.ConventionalControllers.Create(typeof(SchrodingerServerHttpApiModule).Assembly);
            });
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
        
        private static void ConfigureSwaggerServices(ServiceConfigurationContext context, IConfiguration configuration)
        {
            context.Services.AddAbpSwaggerGen(options =>
                {
                    options.SwaggerDoc("v1", new OpenApiInfo { Title = "SignServer API", Version = "v1" });
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
                }
            );
        }

    }
}