using System.Collections.Generic;
using System.Threading.Tasks;
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

    [HttpPost("item/level")]
    public async Task<List<RankData>> GetItemLevelInfoAsync(GetLevelInfoInputDto input)
    {
        return await _levelProvider.GetItemLevelAsync(input);
    }
}