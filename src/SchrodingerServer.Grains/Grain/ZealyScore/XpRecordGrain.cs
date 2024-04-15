using Microsoft.Extensions.Logging;
using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.Grains.Grain.ZealyScore.Dtos;
using SchrodingerServer.Grains.State.ZealyScore;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Grains.Grain.ZealyScore;

public interface IXpRecordGrain : IGrainWithStringKey
{
    Task<GrainResultDto<XpRecordGrainDto>> CreateAsync(XpRecordGrainDto input);

    Task<GrainResultDto<XpRecordGrainDto>> HandleRecordAsync(RecordInfo input, string userId,
        string address);

    Task<GrainResultDto<XpRecordGrainDto>> GetAsync();
    Task<GrainResultDto<XpRecordGrainDto>> SetStatusToPendingAsync(string bizId);
    Task<GrainResultDto<XpRecordGrainDto>> SetFinalStatusAsync(string status, string remark);
}

public class XpRecordGrain : Grain<XpRecordState>, IXpRecordGrain
{
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<XpRecordGrain> _logger;

    public XpRecordGrain(IObjectMapper objectMapper, ILogger<XpRecordGrain> logger)
    {
        _objectMapper = objectMapper;
        _logger = logger;
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

    public async Task<GrainResultDto<XpRecordGrainDto>> CreateAsync(XpRecordGrainDto input)
    {
        var result = new GrainResultDto<XpRecordGrainDto>();

        var userXpGrain = GrainFactory.GetGrain<IZealyUserXpGrain>(input.UserId);
        // update xp
        var updateResult = await userXpGrain.UpdateXpAsync(input.CurrentXp, input.IncreaseXp, input.PointsAmount);
        if (!updateResult.Success)
        {
            result.Success = false;
            result.Message = updateResult.Message;
            return result;
        }

        if (!State.Id.IsNullOrEmpty() && State.Status == ContractInvokeStatus.ToBeCreated.ToString())
        {
            return Success();
        }

        if (!State.Id.IsNullOrEmpty() && State.Status != ContractInvokeStatus.ToBeCreated.ToString())
        {
            result.Success = false;
            result.Message = "record already exist.";
            return result;
        }

        State = _objectMapper.Map<XpRecordGrainDto, XpRecordState>(input);
        State.Id = this.GetPrimaryKeyString();
        State.Status = ContractInvokeStatus.ToBeCreated.ToString();
        State.CreateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        State.UpdateTime = State.CreateTime;
        await WriteStateAsync();

        // clear record info.
        try
        {
            await userXpGrain.ClearRecordInfo(DateTime.UtcNow.ToString("yyyy-MM-dd"));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "ClearRecordInfo error, userId:{userId}", State.UserId);
        }

        return Success();
    }

    public async Task<GrainResultDto<XpRecordGrainDto>> HandleRecordAsync(RecordInfo input, string userId,
        string address)
    {
        await ReadStateAsync();
        if (!State.Id.IsNullOrEmpty())
        {
            var recordDto = _objectMapper.Map<XpRecordState, XpRecordGrainDto>(State);
            recordDto.Status = ContractInvokeStatus.ToBeCreated.ToString();

            await ClearRecordsAsync(userId, input.Date);
            return new GrainResultDto<XpRecordGrainDto>()
            {
                Success = true,
                Data = recordDto
            };
        }

        State.Id = this.GetPrimaryKeyString();
        State.IncreaseXp = input.IncreaseXp;
        State.CurrentXp = input.CurrentXp;
        State.PointsAmount = input.PointsAmount;
        State.UserId = userId;
        State.Address = address;
        State.Status = ContractInvokeStatus.ToBeCreated.ToString();
        State.CreateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        State.UpdateTime = State.CreateTime;
        await WriteStateAsync();

        // update xp
        await ClearRecordsAsync(userId, input.Date);
        return Success();
    }

    private async Task ClearRecordsAsync(string userId, string date)
    {
        try
        {
            var userXpGrain = GrainFactory.GetGrain<IZealyUserXpGrain>(userId);
            await userXpGrain.ClearRecordInfo(date);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "ClearRecordInfo error, userId:{userId}", userId);
        }
    }

    public Task<GrainResultDto<XpRecordGrainDto>> GetAsync()
    {
        if (State.Id.IsNullOrEmpty())
        {
            return Task.FromResult(Fail("record not exist."));
        }

        return Task.FromResult(Success());
    }

    public async Task<GrainResultDto<XpRecordGrainDto>> SetStatusToPendingAsync(string bizId)
    {
        if (State.Id.IsNullOrEmpty())
        {
            return Fail("record not exist.");
        }

        if (State.Status == ContractInvokeStatus.Pending.ToString())
        {
            return Success();
        }

        if (State.Status != ContractInvokeStatus.ToBeCreated.ToString())
        {
            return Fail("record status is not ToBeCreated.");
        }

        State.Status = ContractInvokeStatus.Pending.ToString();
        State.BizId = bizId;
        State.UpdateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await WriteStateAsync();

        return Success();
    }

    public async Task<GrainResultDto<XpRecordGrainDto>> SetFinalStatusAsync(string status, string remark)
    {
        if (State.Id.IsNullOrEmpty())
        {
            return Fail("record not exist.");
        }

        if (State.Status == ContractInvokeStatus.ToBeCreated.ToString() ||
            status == ContractInvokeStatus.ToBeCreated.ToString() ||
            status == ContractInvokeStatus.Pending.ToString())
        {
            return Fail("status can not change.");
        }

        if (State.Status == ContractInvokeStatus.Success.ToString() ||
            State.Status == ContractInvokeStatus.FinalFailed.ToString())
        {
            return Success();
        }

        State.Status = status;
        State.UpdateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (!remark.IsNullOrEmpty())
        {
            State.Remark = State.Remark == null ? remark : $"{State.Remark}:{remark}";
        }

        // if (State.Status == ContractInvokeStatus.FinalFailed.ToString())
        // {
        //     // rollback user xp
        //     var userXpGrain = GrainFactory.GetGrain<IZealyUserXpGrain>(State.UserId);
        //     await userXpGrain.RollbackXpAsync(State.IncreaseXp);
        // }

        await WriteStateAsync();
        return Success();
    }

    private GrainResultDto<XpRecordGrainDto> Success()
    {
        return new GrainResultDto<XpRecordGrainDto>()
        {
            Success = true,
            Data = _objectMapper.Map<XpRecordState, XpRecordGrainDto>(State)
        };
    }

    private GrainResultDto<XpRecordGrainDto> Fail(string message)
    {
        return new GrainResultDto<XpRecordGrainDto>()
        {
            Success = false,
            Message = message
        };
    }
}