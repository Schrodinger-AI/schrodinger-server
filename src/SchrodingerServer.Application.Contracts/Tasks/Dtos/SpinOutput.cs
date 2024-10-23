using Google.Protobuf;

namespace SchrodingerServer.Tasks.Dtos;

public class SpinOutput
{
    public string Seed { get; set; }
    public string Tick { get; set; }
    public ByteString Signature { get; set; }
    public long ExpirationTime { get; set; }
}