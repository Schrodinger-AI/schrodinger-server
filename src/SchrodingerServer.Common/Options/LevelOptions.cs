namespace SchrodingerServer.Options;

public class LevelOptions
{
    public string ChainId { get; set; }
    public string SchrodingerUrl { get; set; } 
    
    public string AwakenUrl { get; set; } 
    
    public string S3LevelFileKeyName { get; set; }
    public int BatchQuerySize { get; set; } = 100;
    
    public string ChainIdForReal { get; set; } 
    public string ForestUrl { get; set; } 
    public long  AdoptTime { get; set; }
}