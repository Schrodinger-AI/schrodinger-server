using Google.Protobuf;

namespace SchrodingerServer.Tasks.Dtos;

public class VoucherAdoptionOutput
{
    public string VoucherId { get; set; }
    public ByteString Signature { get; set; }
    public bool IsRare { get; set; }
}