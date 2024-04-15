using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SchrodingerServer.Config;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace SchrodingerServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("ConfigController")]
[Route("api/app/config")]
public class ConfigController : AbpController
{
    private readonly IConfigAppService _configAppService;

    public ConfigController(IConfigAppService configAppService)
    {
        _configAppService = configAppService;
    }

    [HttpGet]
    public Dictionary<string, string> GetConfig()
    {
        return _configAppService.GetConfig();
    }
   
}