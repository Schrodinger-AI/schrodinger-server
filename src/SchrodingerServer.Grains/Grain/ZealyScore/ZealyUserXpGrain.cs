using Orleans;
using SchrodingerServer.Grains.Grain.ZealyScore.Dtos;
using SchrodingerServer.Grains.State.ZealyScore;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Grains.Grain.ZealyScore;

public interface IZealyUserXpGrain : IGrainWithStringKey
{
    Task<GrainResultDto<ZealyUserXpGrainDto>> AddUserXpInfoAsync(ZealyUserXpGrainDto input);
    Task<GrainResultDto<ZealyUserXpGrainDto>> GetUserXpInfoAsync();
    Task<GrainResultDto<ZealyUserXpGrainDto>> UpdateXpAsync(decimal currentXp, decimal sendXp, decimal sendAmount);
    Task<GrainResultDto<ZealyUserXpGrainDto>> ClearRecordInfo(string date);
}

public class ZealyUserXpGrain : Grain<ZealyUserXpState>, IZealyUserXpGrain
{
    private readonly IObjectMapper _objectMapper;

    public ZealyUserXpGrain(IObjectMapper objectMapper)
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

    public async Task<GrainResultDto<ZealyUserXpGrainDto>> AddUserXpInfoAsync(ZealyUserXpGrainDto input)
    {
        var result = new GrainResultDto<ZealyUserXpGrainDto>();

        if (!State.Id.IsNullOrEmpty())
        {
            result.Success = false;
            result.Message = "user xp info already exist.";
            return result;
        }

        State.Id = this.GetPrimaryKeyString();
        State.Address = input.Address;

        State.CreateTime = DateTime.UtcNow;
        State.UpdateTime = State.CreateTime;
        await WriteStateAsync();

        return new GrainResultDto<ZealyUserXpGrainDto>()
        {
            Success = true,
            Data = _objectMapper.Map<ZealyUserXpState, ZealyUserXpGrainDto>(State)
        };
    }

    public async Task<GrainResultDto<ZealyUserXpGrainDto>> UpdateXpAsync(decimal currentXp, decimal sendXp,
        decimal sendAmount)
    {
        var result = new GrainResultDto<ZealyUserXpGrainDto>();

        if (State.Id.IsNullOrEmpty())
        {
            result.Success = false;
            result.Message = ZealyErrorMessage.UserXpInfoNotExistCode;
            return result;
        }

        if (State.CurrentXp == currentXp)
        {
            return Success();
        }

        State.RecordInfos.Add(new RecordInfo()
        {
            Date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            CurrentXp = currentXp,
            IncreaseXp = sendXp,
            PointsAmount = sendAmount
        });
        
        State.LastXp = State.CurrentXp;
        State.CurrentXp = currentXp;

        State.IsRollback = false;
        State.UpdateTime = DateTime.UtcNow;
        await WriteStateAsync();

        return Success();
    }

    public async Task<GrainResultDto<ZealyUserXpGrainDto>> ClearRecordInfo(string date)
    {
        var recordInfo = State.RecordInfos.FirstOrDefault(t => t.Date == date);
        if (recordInfo != null)
        {
            State.RecordInfos.Remove(recordInfo);
        }

        State.UpdateTime = DateTime.UtcNow;
        await WriteStateAsync();
        return Success();
    }

    public Task<GrainResultDto<ZealyUserXpGrainDto>> GetUserXpInfoAsync()
    {
        var result = new GrainResultDto<ZealyUserXpGrainDto>();
        if (State.Id.IsNullOrEmpty())
        {
            result.Success = false;
            result.Message = ZealyErrorMessage.UserXpInfoNotExistCode;
            return Task.FromResult(result);
        }

        return Task.FromResult(new GrainResultDto<ZealyUserXpGrainDto>()
        {
            Success = true,
            Data = _objectMapper.Map<ZealyUserXpState, ZealyUserXpGrainDto>(State)
        });
    }

    private GrainResultDto<ZealyUserXpGrainDto> Success()
    {
        return new GrainResultDto<ZealyUserXpGrainDto>()
        {
            Success = true,
            Data = _objectMapper.Map<ZealyUserXpState, ZealyUserXpGrainDto>(State)
        };
    }
}