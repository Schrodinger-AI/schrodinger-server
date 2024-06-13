using Volo.Abp.EventBus;

namespace SchrodingerServer.Activity.Eto;

[EventName("UpdateAddressCacheEto")]
public class UpdateAddressCacheEto
{
    public long BeginTime { get; set; }
    public  long EndTime { get; set; }
}