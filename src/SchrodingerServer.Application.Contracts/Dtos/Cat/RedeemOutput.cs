using Google.Protobuf;

namespace SchrodingerServer.Dtos.Cat;

public class RedeemOutput
{
    public string Tick { get; set; }
    public string AdoptId { get; set; }
    public long Level { get; set; }
    public ByteString Signature { get; set; } 
}