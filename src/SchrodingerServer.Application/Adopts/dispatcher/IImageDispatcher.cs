using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Dtos.TraitsDto;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Adopts.dispatcher;

public interface IImageDispatcher
{
    Task DispatchAIGenerationRequest(string aelfAddress, GenerateImage imageInfo, string adoptId);
    IImageProvider CurrentProvider();
}

public class ImageGenerationIdDto
{
    public string ImageGenerationId { get; set; }
    public bool Exist { get; set; }
}

public class ImageDispatcher : IImageDispatcher, ISingletonDependency
{
    private readonly AdoptImageOptions _adoptImageOptions;
    private readonly ILogger<ImageDispatcher> _logger;
    private readonly Dictionary<string, IImageProvider> _providers;

    public ImageDispatcher(IOptionsMonitor<AdoptImageOptions> adoptImageOptions, ILogger<ImageDispatcher> logger, IEnumerable<IImageProvider> providers)
    {
        _adoptImageOptions = adoptImageOptions.CurrentValue;
        _logger = logger;
        _providers = providers.ToDictionary(x => x.Type.ToString(), y => y);
    }

    public async Task DispatchAIGenerationRequest(string adoptAddressId, GenerateImage imageInfo, string adoptId)
    {
        var provider = CurrentProvider();
        _logger.LogInformation("GenerateImageByAiAsync Begin. imageInfo: {info} adoptId: {adoptId} ", JsonConvert.SerializeObject(imageInfo), adoptId);
        await provider.SendAIGenerationRequestAsync(adoptAddressId, adoptId, imageInfo);
    }

    public IImageProvider CurrentProvider()
    {
        if (!_providers.TryGetValue(_adoptImageOptions.ImageProvider, out var provider))
        {
            _logger.LogError("Get AI Provider Failed");
            throw new UserFriendlyException("wrong type of image provider configuration");
        }

        return provider;
    }
}