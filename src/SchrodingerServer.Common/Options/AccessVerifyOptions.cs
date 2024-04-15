namespace SchrodingerServer.Common.Options;

public class AccessVerifyOptions
{

    public string HostHeader { get; set; } = "Host";

    public int DomainCacheSeconds { get; set; } = 1800;
    public List<string> HostWhiteList { get; set; } = new();
    
}