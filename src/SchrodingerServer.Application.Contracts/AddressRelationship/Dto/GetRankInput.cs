namespace SchrodingerServer.AddressRelationship.Dto;

public class GetRankInput
{
    public bool UpdateAddressCache { get; set; } = false;
    public bool IsFinal { get; set; } = false;
}