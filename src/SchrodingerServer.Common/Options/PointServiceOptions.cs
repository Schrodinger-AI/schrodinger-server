namespace SchrodingerServer.Common.Options;

public class PointServiceOptions
{
    public string BaseUrl { get; set; }
    public string DappName { get; set; }
    public string DappSecret { get; set; }
    public string DappId { get; set; }
    
    public string EcoEarnUrl { get; set; }

    public bool EcoEarnReady { get; set; } = false;
    public string Address { get; set; }
}