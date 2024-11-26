namespace SchrodingerServer.Grains.Grain.Traits;

[GenerateSerializer]
public class AdoptImageInfoState
{
    [Id(0)]
    public string ImageGenerationId { get; set; }

    [Id(1)]
    public List<string> Images { get; set; }

    [Id(2)]
    public bool HasWatermark { get; set; }

    [Id(3)]
    public string ResizedImage { get; set; }

    [Id(4)]
    public string ImageUri { get; set; }

    [Id(5)]
    public bool HasSendRequest { get; set; }
}