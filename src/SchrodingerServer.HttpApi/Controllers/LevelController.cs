using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SchrodingerServer.Dto;
using SchrodingerServer.Users;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace SchrodingerServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Users")]
[Route("api/app")]
public class LevelController : AbpController
{
    private readonly ILevelProvider _levelProvider;

    public LevelController(ILevelProvider levelProvider)
    {
        _levelProvider = levelProvider;
    }
    
    [HttpGet("whitelist/{address}")]
    public async Task<bool> GetItemLevelDicAsync(string address)
    {
        return await _levelProvider.CheckAddressIsInWhiteListAsync(address);
    }
}