using Google.Protobuf;

namespace SchrodingerServer.Tasks.Dtos;

public class SpinOutput
{
    public string Seed { get; set; }
    public string Tick { get; set; }
    public ByteString Signature { get; set; }
    public long ExpirationTime { get; set; }
}

public class SpinOutputCache
{
    public string Seed { get; set; }
    public string Tick { get; set; }
    public string Signature { get; set; }
    public long ExpirationTime { get; set; }
}
