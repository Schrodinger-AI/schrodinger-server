using System.Collections.Generic;
using Google.Protobuf;

namespace SchrodingerServer.Dtos.Cat;

public class CombineOutput
{
    public List<string> AdoptIds { get; set; }
    public long Level { get; set; }
    public ByteString Signature { get; set; } 
}