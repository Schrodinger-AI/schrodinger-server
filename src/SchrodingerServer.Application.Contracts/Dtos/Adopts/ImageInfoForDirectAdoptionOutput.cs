namespace SchrodingerServer.Dtos.Adopts;

public class ImageInfoForDirectAdoptionOutput
{
    public string Image { get; set; }
    
    public string Signature { get; set; }
    public string ImageUri { get; set; }
    public AdoptImageInfo AdoptImageInfo { get; set; }
    public bool UnderMaintenance { get; set; } = false;
    
}