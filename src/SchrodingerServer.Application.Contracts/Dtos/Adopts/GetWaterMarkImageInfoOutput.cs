using Orleans;

namespace SchrodingerServer.Dtos.Adopts;

public class GetWaterMarkImageInfoOutput
{
    public string Image { get; set; }
    
    public string Signature { get; set; }
    public string ImageUri { get; set; }
}


[GenerateSerializer]
public class WaterImageGrainInfoDto
{
    [Id(0)]
    public string ResizedImage { get; set; }

    [Id(1)]
    public string ImageUri { get; set; }
} 