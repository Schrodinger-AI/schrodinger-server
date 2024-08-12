namespace SchrodingerServer.AddressRelationship.Dto;

public class GetBotRankInput
{
    public int Tab { get; set; }
    public bool IsCurrent { get; set; }
    public string Address { get; set; }
}