namespace SchrodingerServer.PointServer.Dto;

public class InvitationRequest
{
    
    public string DappName { get; set; }
    public string Address { get; set; }
    public long InviteTime { get; set; }
    public string Domain { get; set; }
    public string Signature { get; set; }
    
}