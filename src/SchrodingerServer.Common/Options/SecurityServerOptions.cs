namespace SchrodingerServer.Common.Options;

public class SecurityServerOptions
{
    public string BaseUrl { get; set; }
    public string AppId { get; set; }
    public string AppSecret { get; set; }
    public int SecretCacheSeconds { get; set; } = 60;
    public KeyIds KeyIds { get; set; } = new();
}

public class KeyIds
{

}