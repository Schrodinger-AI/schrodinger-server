using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.Grains.State.Users;
using SchrodingerServer.Users;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Grains.Grain.Users;

public interface IUserGrain : IGrainWithGuidKey
{

    Task<GrainResultDto<UserGrainDto>> UpdateUserAsync(UserGrainDto input);

    Task<GrainResultDto<UserGrainDto>> GetUserAsync();

}

public class UserGrain : Grain<UserState>, IUserGrain
{
    private readonly IObjectMapper _objectMapper;

    public UserGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
    }
    
    public override async Task OnActivateAsync()
    {
        await ReadStateAsync();
        await base.OnActivateAsync();
    }

    public override async Task OnDeactivateAsync()
    {
        await WriteStateAsync();
        await base.OnDeactivateAsync();
    }
    
    public async Task<GrainResultDto<UserGrainDto>> UpdateUserAsync(UserGrainDto input)
    {
        var registerDomain = State.RegisterDomain;
        State = _objectMapper.Map<UserGrainDto, UserState>(input);
        if (State.Id == Guid.Empty)
        {
            State.Id = this.GetPrimaryKey();
        }

        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        State.CreateTime = State.CreateTime == 0 ? now : State.CreateTime;
        State.UpdateTime = now;
        
        if (registerDomain.IsNullOrEmpty())
        {
            State.RegisterDomain = input.RegisterDomain;
        }
        
        await WriteStateAsync();

        return new GrainResultDto<UserGrainDto>()
        {
            Success = true,
            Data = _objectMapper.Map<UserState, UserGrainDto>(State)
        };
    }

    public Task<GrainResultDto<UserGrainDto>> GetUserAsync()
    {
        return Task.FromResult(new GrainResultDto<UserGrainDto>()
        {
            Success = true,
            Data = _objectMapper.Map<UserState, UserGrainDto>(State)
        });
    }
}