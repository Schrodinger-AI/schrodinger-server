using System.Collections.Generic;
using Microsoft.Extensions.Options;
using SchrodingerServer.Common.Options;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Config;

public class ConfigAppService : IConfigAppService, ITransientDependency
{

    private readonly IOptionsMonitor<CmsConfigOptions> _cmsConfigOptions;

    public ConfigAppService(IOptionsMonitor<CmsConfigOptions> cmsConfigOptions)
    {
        _cmsConfigOptions = cmsConfigOptions;
    }

    public Dictionary<string, string> GetConfig()
    {
        return _cmsConfigOptions.CurrentValue.ConfigMap;
    }
}