using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using Microsoft.Extensions.Logging;
using Orleans;
using SchrodingerServer.Dtos.Adopts;
using SchrodingerServer.ExceptionHandling;
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

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionStr))]
    public async Task<string> GetRequestIdAsync(string adoptId)
    {
        var grain = _clusterClient.GetGrain<IAdoptImageInfoGrain>(adoptId);
        return await grain.GetImageGenerationIdAsync();
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionStr))]
    public async Task<string> SetImageGenerationIdNXAsync(string adoptId, string imageGenerationId)
    {
        var grain = _clusterClient.GetGrain<IAdoptImageInfoGrain>(adoptId);
        return await grain.SetImageGenerationIdNXAsync(imageGenerationId);
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

    public async Task SetWatermarkImageInfoAsync(string adoptId, string imageUri, string resizedImage, string selectedImage, bool needRemove)
    {
        var grain = _clusterClient.GetGrain<IAdoptImageInfoGrain>(adoptId);
        await grain.SetWatermarkImageInfoAsync(imageUri, resizedImage);

        if (needRemove)
        {
            var images = await grain.GetImagesAsync();
            var index = images.IndexOf(selectedImage);
        
            // only works when there are two images in the list
            images.RemoveAt((index+1) % 2);
            await grain.SetImagesAsync(images);
        }
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