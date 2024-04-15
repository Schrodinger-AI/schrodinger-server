using SchrodingerServer.Dtos.TraitsDto;
using Volo.Abp.EventBus;

namespace SchrodingerServer.Image;

[EventName("DefaultImageGenerateEto")]
public class DefaultImageGenerateEto
{
    public string AdoptId { get; set; }
    public string AdoptAddressId { get; set; }

    public GenerateImage GenerateImage { get; set; }
}