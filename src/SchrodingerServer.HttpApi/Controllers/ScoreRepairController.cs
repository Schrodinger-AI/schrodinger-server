using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchrodingerServer.ScoreRepair;
using SchrodingerServer.ScoreRepair.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace SchrodingerServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("XpScoreRepair")]
[Route("api/app/repair")]
public class ScoreRepairController : AbpControllerBase
{
    private readonly IXpScoreRepairAppService _repairAppService;

    public ScoreRepairController(IXpScoreRepairAppService repairAppService)
    {
        _repairAppService = repairAppService;
    }

    [Authorize(Roles = "admin")]
    [HttpPost("xp-score")]
    public async Task UpdateScoreRepairDataAsync(List<UpdateXpScoreRepairDataDto> input)
    {
        await _repairAppService.UpdateScoreRepairDataAsync(input);
    }

    [Authorize(Roles = "admin")]
    [HttpGet("xp-infos")]
    public async Task<XpScoreRepairDataPageDto> GetXpScoreRepairDataAsync(XpScoreRepairDataRequestDto input)
    {
        return await _repairAppService.GetXpScoreRepairDataAsync(input);
    }

    [Authorize(Roles = "admin")]
    [HttpGet("user-xp-info")]
    public async Task<UserXpInfoDto> GetUserXpAsync(UserXpInfoRequestDto input)
    {
        return await _repairAppService.GetUserXpAsync(input);
    }

    [Authorize(Roles = "admin")]
    [HttpGet("user-records")]
    public async Task<XpRecordPageResultDto> GetUserRecordsAsync(string userId, int skipCount, int maxResultCount)
    {
        return await _repairAppService.GetUserRecordsAsync(userId, skipCount, maxResultCount);
    }

    [Authorize(Roles = "admin")]
    [HttpPost("re-create-contract")]
    public async Task ReCreateContractAsync(ReCreateDto input)
    {
        await _repairAppService.ReCreateContractAsync(input);
    }
}