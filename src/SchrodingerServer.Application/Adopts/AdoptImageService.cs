using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using SchrodingerServer.Dtos.Adopts;
using SchrodingerServer.Grains.Grain.Traits;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Adopts;

public class AdoptImageService : IAdoptImageService, ISingletonDependency
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<AdoptImageService> _logger;

    public AdoptImageService(IClusterClient clusterClient, ILogger<AdoptImageService> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    public async Task<string> GetRequestIdAsync(string adoptId)
    {
        try
        {
            var grain = _clusterClient.GetGrain<IAdoptImageInfoGrain>(adoptId);
            return await grain.GetImageGenerationIdAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetRequestExistAsync Exception adoptId:{AdoptId}", adoptId);
            return string.Empty;
        }
    }

    public async Task<string> SetImageGenerationIdNXAsync(string adoptId, string imageGenerationId)
    {
        try
        {
            var grain = _clusterClient.GetGrain<IAdoptImageInfoGrain>(adoptId);
            return await grain.SetImageGenerationIdNXAsync(imageGenerationId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "SetRequestAsync Exception adoptId:{AdoptId}, imageGenerationId:{ImageGenerationId}", adoptId, imageGenerationId);
            return "invalidVal";
        }
    }

    public async Task<List<string>> GetImagesAsync(string adoptId)
    {
        var grain = _clusterClient.GetGrain<IAdoptImageInfoGrain>(adoptId);
        return await grain.GetImagesAsync();
    }

    public async Task SetImagesAsync(string adoptId, List<string> images)
    {
        var grain = _clusterClient.GetGrain<IAdoptImageInfoGrain>(adoptId);
        await grain.SetImagesAsync(images);
    }

    public async Task SetWatermarkAsync(string adoptId)
    {
        var grain = _clusterClient.GetGrain<IAdoptImageInfoGrain>(adoptId);
        await grain.SetWatermarkAsync();
    }

    public async Task<bool> HasWatermark(string adoptId)
    {
        var grain = _clusterClient.GetGrain<IAdoptImageInfoGrain>(adoptId);
        return await grain.HasWatermarkAsync();
    }

    public async Task SetWatermarkImageInfoAsync(string adoptId, string imageUri, string resizedImage, string selectedImage)
    {
        var grain = _clusterClient.GetGrain<IAdoptImageInfoGrain>(adoptId);
        await grain.SetWatermarkImageInfoAsync(imageUri, resizedImage);

        var images = await grain.GetImagesAsync();
        var index = images.IndexOf(selectedImage);
        
        // only works when there are two images in the list
        images.RemoveAt((index+1) % 2);
        await grain.SetImagesAsync(images);
    }

    public async Task<WaterImageGrainInfoDto> GetWatermarkImageInfoAsync(string adoptId)
    {
        var grain = _clusterClient.GetGrain<IAdoptImageInfoGrain>(adoptId);
        var grainResult = await grain.GetWatermarkImageInfoAsync();
        return grainResult.Data;
    }
    
    public Task<bool> HasSendRequest(string adoptId)
    {
        var grain = _clusterClient.GetGrain<IAdoptImageInfoGrain>(adoptId);
        return grain.HasSendRequest();
    }

    public async Task MarkRequest(string adoptId)
    {
        var grain = _clusterClient.GetGrain<IAdoptImageInfoGrain>(adoptId);
        await grain.MarkRequest();
    }
}