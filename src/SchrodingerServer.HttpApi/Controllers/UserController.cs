using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchrodingerServer.Common;
using SchrodingerServer.Dto;
using SchrodingerServer.Users;
using SchrodingerServer.Users.Dto;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace SchrodingerServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Users")]
[Route("api/app")]
[Authorize]
public class UserController : AbpController
{
    private readonly IUserActionProvider _userActionProvider;

    public UserController(IUserActionProvider userActionProvider)
    {
        _userActionProvider = userActionProvider;
    }

    [HttpGet("user/info")]
    public async Task<UserInfoDto> GetUserInfo()
    {
        var joinTime = await _userActionProvider.GetActionTimeAsync(ActionType.Join);
        return new UserInfoDto()
        {
            IsJoin = joinTime != null
        };
    }


    [HttpPost("join")]
    public async Task<UserInfoDto> DoJoin()
    {
        var grainDto = await _userActionProvider.AddActionAsync(ActionType.Join);
        return new UserInfoDto
        {
            IsJoin = grainDto.ActionData.ContainsKey(ActionType.Join.ToString())
        };
    }

    [HttpGet("my/points")]
    public async Task<MyPointDetailsDto> GetMyPointsAsync(GetMyPointsInput input)
    {
        return await _userActionProvider.GetMyPointsAsync(input);
    }
}