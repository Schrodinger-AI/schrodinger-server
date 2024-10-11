using System.Collections.Generic;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchrodingerServer.Activity;
using SchrodingerServer.AddressRelationship;
using SchrodingerServer.AddressRelationship.Dto;
using SchrodingerServer.Users.Dto;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace SchrodingerServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Activity")]
[Route("api/app/activity")]
public class ActivityController : AbpController
{
    private readonly IActivityApplicationService _activityApplicationService;
    
    public ActivityController(IActivityApplicationService activityApplicationService)
    {
        _activityApplicationService = activityApplicationService;
    }
    
    [HttpGet("list")]
    public Task<ActivityListDto> GetActivityListAsync(GetActivityListInput input)
    {
        return  _activityApplicationService.GetActivityListAsync(input);
    }
    
    [HttpGet("info")]
    public Task<ActivityInfoDto> GetActivityInfoAsync()
    {
        return  _activityApplicationService.GetActivityInfoAsync();
    }
    
    [HttpPost("bind-address")]
    [Authorize]
    public Task BindAddressAsync(BindActivityAddressInput input)
    {
        return  _activityApplicationService.BindActivityAddressAsync(input);
    }
    
    [HttpPost("address-relation")]
    public Task<ActivityAddressDto> GetActivityAddressAsync(GetActivityAddressInput input)
    {
        return  _activityApplicationService.GetActivityAddressAsync(input);
    }
    
    [HttpPost("rank")]
    public Task<RankDto> GetRankAsync(GetRankInput input)
    {
        return _activityApplicationService.GetRankAsync(input);
    }
    
    [HttpGet("stage")]
    public Task<StageDto> GetStageAsync(GetStageInput input)
    {
        return  _activityApplicationService.GetStageAsync(input.ActivityId);
    }
    
    
    [HttpPost("bot-rank")]
    public Task<BotRankDto> GetBotRankAsync(GetBotRankInput input)
    {
        return _activityApplicationService.GetBotRankAsync(input);
    }
}