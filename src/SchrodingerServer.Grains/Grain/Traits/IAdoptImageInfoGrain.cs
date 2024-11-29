using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Dtos.Adopts;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Grains.Grain.Traits;

public interface IAdoptImageInfoGrain : IGrainWithStringKey
{
    Task<string> SetImageGenerationIdNXAsync(string imageGenerationId);
    Task SetImagesAsync(List<string> images);
    Task<string> GetImageGenerationIdAsync();
    Task<List<string>> GetImagesAsync();
    Task SetWatermarkAsync();
    Task<bool> HasWatermarkAsync();
    Task SetWatermarkImageInfoAsync(string uri, string resizeImage);

    Task<GrainResultDto<WaterImageGrainInfoDto>> GetWatermarkImageInfoAsync();

    Task<bool> HasSendRequest();
    Task MarkRequest();
}

public class AdoptImageInfoGrain : Grain<AdoptImageInfoState>, IAdoptImageInfoGrain
{
    private readonly ILogger<AdoptImageInfoGrain> _logger;
    private readonly IOptionsMonitor<ChainOptions> _chainOptionsMonitor;
    private readonly IObjectMapper _objectMapper;


    public AdoptImageInfoGrain(ILogger<AdoptImageInfoGrain> logger, IObjectMapper objectMapper,
        IOptionsMonitor<ChainOptions> chainOptionsMonitor)
    {
        _logger = logger;
        _objectMapper = objectMapper;
        _chainOptionsMonitor = chainOptionsMonitor;
    }


    public async Task<string> SetImageGenerationIdNXAsync(string imageGenerationId)
    {
        if (State.ImageGenerationId.IsNullOrEmpty())
        {
            State.ImageGenerationId = imageGenerationId;
            await WriteStateAsync();
        }

        return State.ImageGenerationId;
    }

    public async Task SetImagesAsync(List<string> images)
    {
        State.Images = images;
        await WriteStateAsync();
    }

    public Task<string> GetImageGenerationIdAsync()
    {
        return Task.FromResult(State.ImageGenerationId);
    }

    public Task<List<string>> GetImagesAsync()
    {
        return Task.FromResult(State.Images);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        await WriteStateAsync();
        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.ReadStateAsync();
        await base.OnActivateAsync(cancellationToken);
    }

    public async Task SetWatermarkAsync()
    {
        State.HasWatermark = true;
        await WriteStateAsync();
    }

    public Task<bool> HasWatermarkAsync()
    {
        return Task.FromResult(State.HasWatermark);
    }

    public async Task SetWatermarkImageInfoAsync(string uri, string resizeImage)
    {
        State.ImageUri = uri;
        State.ResizedImage = resizeImage;
        State.HasWatermark = true;
        await WriteStateAsync();
    }


    public async Task<GrainResultDto<WaterImageGrainInfoDto>> GetWatermarkImageInfoAsync()
    {
        return new GrainResultDto<WaterImageGrainInfoDto>
        {
            Success = true,
            Data = _objectMapper.Map<AdoptImageInfoState, WaterImageGrainInfoDto>(State)
        };
    }

    public Task<bool> HasSendRequest()
    {
        return Task.FromResult(State.HasSendRequest);
    }

    public async Task MarkRequest()
    {
        State.HasSendRequest = true;
        await WriteStateAsync();
    }
}