namespace SchrodingerServer.Grains.Grain.Traits;

public class AdoptImageInfoState
{
    public string ImageGenerationId { get; set; }
    
    public List<string> Images { get; set; }
    
    public bool HasWatermark { get; set; }
    
    public string ResizedImage { get; set; }
    
    public string ImageUri { get; set; }
    
    public bool HasSendRequest { get; set; }
}