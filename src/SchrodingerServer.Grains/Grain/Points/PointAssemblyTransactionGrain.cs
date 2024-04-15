using Orleans;
using SchrodingerServer.Grains.State.Points;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Grains.Grain.Points;


public interface IPointAssemblyTransactionGrain : IGrainWithStringKey
{
    Task<GrainResultDto<PointAssemblyTransactionGrainDto>> CreateAsync(PointAssemblyTransactionGrainDto input);
    
    Task<GrainResultDto<PointAssemblyTransactionGrainDto>> GetAsync();
}

public class PointAssemblyTransactionGrain  : Grain<PointAssemblyTransactionState>, IPointAssemblyTransactionGrain
{
    private readonly IObjectMapper _objectMapper;
    
    public PointAssemblyTransactionGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
    }

    public async Task<GrainResultDto<PointAssemblyTransactionGrainDto>> CreateAsync(PointAssemblyTransactionGrainDto input)
    {
        State = _objectMapper.Map<PointAssemblyTransactionGrainDto, PointAssemblyTransactionState>(input);
        State.CreateTime = DateTime.UtcNow;
        
        await WriteStateAsync();
        
        return new GrainResultDto<PointAssemblyTransactionGrainDto>
        {
            Success = true,
            Data = _objectMapper.Map<PointAssemblyTransactionState, PointAssemblyTransactionGrainDto>(State)
        };    
    }

    public Task<GrainResultDto<PointAssemblyTransactionGrainDto>> GetAsync()
    {
        return Task.FromResult(new GrainResultDto<PointAssemblyTransactionGrainDto>()
        {
            Success = true,
            Data = _objectMapper.Map<PointAssemblyTransactionState, PointAssemblyTransactionGrainDto>(State)
        });
    }
}