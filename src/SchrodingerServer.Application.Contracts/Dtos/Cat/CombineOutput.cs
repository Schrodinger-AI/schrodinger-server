using System.Collections.Generic;
using Google.Protobuf;

namespace SchrodingerServer.Dtos.Cat;

public class CombineOutput
{
    public string Tick { get; set; }
    public List<string> AdoptIds { get; set; }
    public long Level { get; set; }
    public ByteString Signature { get; set; } 
}