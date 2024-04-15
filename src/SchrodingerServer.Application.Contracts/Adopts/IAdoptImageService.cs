using System.Collections.Generic;
using System.Threading.Tasks;
using SchrodingerServer.Dtos.Adopts;

namespace SchrodingerServer.Adopts;

public interface IAdoptImageService
{
    Task<string> GetRequestIdAsync(string adoptId);

    Task<string> SetImageGenerationIdNXAsync(string adoptId, string imageGenerationId);

    Task<List<string>> GetImagesAsync(string adoptId);
    Task SetImagesAsync(string adoptId,List<string> images);

    Task SetWatermarkAsync(string adoptId);
    Task<bool> HasWatermark(string adoptId);

    Task SetWatermarkImageInfoAsync(string adoptId, string imageUri, string resizedImage, string selectedImage);

    Task<WaterImageGrainInfoDto> GetWatermarkImageInfoAsync(string adoptId);
    
    Task<bool> HasSendRequest(string adoptId);
    Task MarkRequest(string adoptId);
}