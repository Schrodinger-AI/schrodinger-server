using System.Collections.Generic;
using Google.Protobuf;

namespace SchrodingerServer.Dtos.Cat;

public class CombineOutput
{
    public List<string> AdoptIds { get; set; }
    public int Level { get; set; }
    public ByteString Signature { get; set; } 
}