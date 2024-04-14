using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Serialization;
using SchrodingerServer.Common;
using SchrodingerServer.Users;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace SchrodingerServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("domain")]
[Route("api/app/domain")]
public class DomainController : AbpController
{
    private readonly IUserActionProvider _userActionProvider;

    public DomainController(IUserActionProvider userActionProvider)
    {
        _userActionProvider = userActionProvider;
    }

    [HttpGet("check")]
    public async Task<string?> DomainCheckAsync()
    {
        var domain = DeviceInfoContext.CurrentDeviceInfo.Host ?? CommonConstant.EmptyString;
        var domainValid = await _userActionProvider.CheckDomainAsync(domain);
        AssertHelper.IsTrue(domainValid, "Invalid host{0}", domain);
        return CommonConstant.Success;
    }
}