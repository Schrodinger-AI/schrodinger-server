using System.Collections.Generic;

namespace SchrodingerServer.Dto;

public class UserReferralRecordDto
{
    public string Domain { get; set; }
    
    public string DappId { get; set; }  
    
    public string Referrer { get; set; }
    
    public string Invitee { get; set; }
    
    public string Inviter { get; set; }

    public long CreateTime { get; set; }
}

public class UserReferralRecordQueryDto
{
    public List<UserReferralRecordDto> GetUserReferralRecordByTime{ get; set; }
}