using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SchrodingerServer.Extension;

public static class ApolloConfigurationExtension
{
    public static IHostBuilder UseApollo(this IHostBuilder builder)
    {
        return builder
            .ConfigureAppConfiguration((config) =>
            {
                var apolloOption = config.Build().GetSection("apollo");
                if (apolloOption.GetSection("UseApollo").Get<bool>())
                {
                    Log.Information("Add apollo AppId:{App} Server:{Server}, Namespaces:{Namespaces}", 
                        apolloOption.GetSection("AppId").Get<string>(),
                        apolloOption.GetSection("MetaServer").Get<string>(),
                        string.Join(",", apolloOption.GetSection("Namespaces").Get<List<string>>())
                        );
                    config.AddApollo(apolloOption);
                }
            });
    }
}