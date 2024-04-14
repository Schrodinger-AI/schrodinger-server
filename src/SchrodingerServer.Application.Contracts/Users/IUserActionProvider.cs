using System;
using System.Threading.Tasks;
using SchrodingerServer.Common;
using SchrodingerServer.Users.Dto;

namespace SchrodingerServer.Users;

public interface IUserActionProvider
{
    Task<bool> CheckDomainAsync(string domain);

    Task<DateTime?> GetActionTimeAsync(ActionType actionType);

    Task<UserActionGrainDto> AddActionAsync(ActionType actionType);

    Task<MyPointDetailsDto> GetMyPointsAsync(GetMyPointsInput input);
    Task<string> GetCurrentUserAddressAsync(string chainId);
}