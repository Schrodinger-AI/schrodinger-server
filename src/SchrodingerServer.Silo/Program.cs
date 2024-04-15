using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SchrodingerServer.Extension;
using SchrodingerServer.Silo.Extensions;
using Serilog;

namespace SchrodingerServer.Silo;

public class Program
{
    public async static Task<int> Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        try
        {
            Log.Information("Starting SchrodingerServer.Silo");
            await CreateHostBuilder(args).RunConsoleAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly!");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    internal static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostcontext, services) =>
            {
                services.AddApplication<SchrodingerServerOrleansSiloModule>();
            })
            .ConfigureAppConfiguration((h,c)=>c.AddJsonFile("apollo.appsettings.json")) 
            .UseApollo()
            .UseOrleansSnapshot()
            .UseAutofac()
            .UseSerilog();
}