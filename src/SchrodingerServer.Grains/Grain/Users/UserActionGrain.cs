using AElf.ExceptionHandler;
using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.ExceptionHandling;
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

public class GrainExceptionHandlingService
{
    public static async Task<FlowBehavior> HandleExceptionDefault(Exception ex)
    {
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return
        };
    }
    
    public static async Task<FlowBehavior> HandleException(Exception ex)
    {
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = 1
        };
    }
    
    public static async Task<FlowBehavior> HandleAElfClientException(Exception ex)
    {
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = 2
        };
    }
    
    public static async Task<FlowBehavior> HandleExceptionFalse(Exception ex)
    {
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
    
    
    
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
    
    [ExceptionHandler(typeof(Exception), ReturnDefault = ReturnDefault.New, TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    public async Task<GrainResultDto<UserActionGrainDto>> AddActionAsync(ActionType actionType)
    {
        if (!State.ActionData.ContainsKey(actionType.ToString()))
        {
            State.ActionData[actionType.ToString()] = DateTime.UtcNow.ToUtcMilliSeconds();
            await WriteStateAsync();
        }
            
        var dto = _objectMapper.Map<UserActionState, UserActionGrainDto>(State);
        return new GrainResultDto<UserActionGrainDto>(dto);
    }
}