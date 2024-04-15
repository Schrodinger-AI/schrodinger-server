using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.Grains.State.Users;
using SchrodingerServer.Users;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Grains.Grain.Users;

public interface IUserActionGrain : IGrainWithGuidKey
{
    Task<DateTime?> GetActionTime(ActionType actionType);
    
    Task<GrainResultDto<UserActionGrainDto>> AddAsync();
    
    Task<GrainResultDto<UserActionGrainDto>> AddActionAsync(ActionType actionType);
}

public class UserActionGrain : Grain<UserActionState>, IUserActionGrain
{

    private readonly IObjectMapper _objectMapper;

    public UserActionGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
    }

    public Task<DateTime?> GetActionTime(ActionType actionType)
    {
        return Task.FromResult<DateTime?>(State.ActionData.TryGetValue(actionType.ToString(), out var actionTimeValue)
            ? TimeHelper.GetDateTimeFromTimeStamp(actionTimeValue)
            : null);
    }

    public Task<GrainResultDto<UserActionGrainDto>> AddAsync()
    {
        var dto = _objectMapper.Map<UserActionState, UserActionGrainDto>(State);
        return Task.FromResult(new GrainResultDto<UserActionGrainDto>(dto));
    }

    public async Task<GrainResultDto<UserActionGrainDto>> AddActionAsync(ActionType actionType)
    {
        try
        {
            if (!State.ActionData.ContainsKey(actionType.ToString()))
            {
                State.ActionData[actionType.ToString()] = DateTime.UtcNow.ToUtcMilliSeconds();
                await WriteStateAsync();
            }
            
            var dto = _objectMapper.Map<UserActionState, UserActionGrainDto>(State);
            return new GrainResultDto<UserActionGrainDto>(dto);
        }
        catch (Exception e)
        {
            return new GrainResultDto<UserActionGrainDto>().Error(e.Message);
        }
    }
}