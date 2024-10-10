using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.Grains.State.Points;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Grains.Grain.Points;

public interface IPointDailyRecordGrain : IGrainWithStringKey
{
    Task<GrainResultDto<PointDailyRecordGrainDto>> UpdateAsync(PointDailyRecordGrainDto input);
    
    Task<GrainResultDto<PointDailyRecordGrainDto>> UpdateAsync(string bizId, string status);
}

public class PointDailyRecordGrain : Grain<PointDailyRecordState>, IPointDailyRecordGrain
{
    private readonly IObjectMapper _objectMapper;

    public PointDailyRecordGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await ReadStateAsync();
        await base.OnActivateAsync(cancellationToken);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        await WriteStateAsync();
        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    /**
     * To Summary amount
     */
    public async Task<GrainResultDto<PointDailyRecordGrainDto>> UpdateAsync(PointDailyRecordGrainDto input)
    {
        // var holderBalanceIds = State.HolderBalanceIds;
        // //If has bizId, can not be modify (The process has entered the packaging and sending transaction)
        // //Each point name and each HolderBalanceId can only be calculated once.
        // if (!State.BizId.IsNullOrEmpty() || holderBalanceIds.Contains(input.HolderBalanceId))
        // {
        //     return OfGrainResultDto(true, CommonConstant.TradeRepeated);
        // }

        var prePointAmount = State.PointAmount;
        State = _objectMapper.Map<PointDailyRecordGrainDto, PointDailyRecordState>(input);
        if (State.CreateTime == DateTime.MinValue)
        {
            State.CreateTime = DateTime.UtcNow;
        }
        State.AddHolderBalanceId(input.HolderBalanceId);
        //accumulated points amount
        State.PointAmount = prePointAmount + input.PointAmount;
        State.UpdateTime = DateTime.UtcNow;

        await WriteStateAsync();

        return OfGrainResultDto(true);
    }

    public async Task<GrainResultDto<PointDailyRecordGrainDto>> UpdateAsync(string bizId, string status)
    {
        State.BizId = bizId;
        State.Status = status;
        State.UpdateTime = DateTime.UtcNow;

        await WriteStateAsync();

        return OfGrainResultDto(true);
    }
    
    private GrainResultDto<PointDailyRecordGrainDto> OfGrainResultDto(bool success, string message = null)
    {
        return new GrainResultDto<PointDailyRecordGrainDto>()
        {
            Data = _objectMapper.Map<PointDailyRecordState, PointDailyRecordGrainDto>(State),
            Success = success,
            Message = message
        };
    }
}