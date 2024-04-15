namespace SchrodingerServer.Dtos.Adopts;

public class GetWaterMarkImageInfoOutput
{
    public string Image { get; set; }
    
    public string Signature { get; set; }
    public string ImageUri { get; set; }
}


public class WaterImageGrainInfoDto
{
    public string ResizedImage { get; set; }
    
    public string ImageUri { get; set; }
} 