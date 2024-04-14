using Orleans;
using SchrodingerServer.Grains.State.Points;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Grains.Grain.Points;


public interface IPointDailyDispatchGrain : IGrainWithStringKey
{
    Task<GrainResultDto<PointDailyDispatchGrainDto>> SavePointDailyDispatch(PointDailyDispatchGrainDto input);
    
    Task<GrainResultDto<PointDailyDispatchGrainDto>> GetPointDailyDispatchGrainAsync();
}

public class PointDailyDispatchGrain : Grain<PointDailyDispatchState>,IPointDailyDispatchGrain
{
    private readonly IObjectMapper _objectMapper;
    public PointDailyDispatchGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
    }
    
    public async Task<GrainResultDto<PointDailyDispatchGrainDto>> SavePointDailyDispatch(PointDailyDispatchGrainDto input)
    {
        State = _objectMapper.Map<PointDailyDispatchGrainDto, PointDailyDispatchState>(input);
        if (State.Id.IsNullOrEmpty())
        {
            State.Id = this.GetPrimaryKey().ToString();
        }
        await WriteStateAsync();

        return new GrainResultDto<PointDailyDispatchGrainDto>
        {
            Success = true,
            Data = _objectMapper.Map<PointDailyDispatchState, PointDailyDispatchGrainDto>(State)
        };
    }

    public Task<GrainResultDto<PointDailyDispatchGrainDto>> GetPointDailyDispatchGrainAsync()
    {
        return Task.FromResult(new GrainResultDto<PointDailyDispatchGrainDto>()
        {
            Success = true,
            Data = _objectMapper.Map<PointDailyDispatchState, PointDailyDispatchGrainDto>(State)
        });
    }
}