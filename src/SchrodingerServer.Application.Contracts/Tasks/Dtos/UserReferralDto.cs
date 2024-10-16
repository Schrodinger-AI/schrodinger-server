using System.Collections.Generic;

namespace SchrodingerServer.Tasks.Dtos;

public class UserReferralDto
{
    public string Domain { get; set; }
    public string DappId { get; set; }
    public string Referrer { get; set; }
    public string Invitee { get; set; }
    public string Inviter { get; set; }
    public long CreateTime { get; set; }
}

public class UserReferralQueryResultDto
{
    public UserReferralRecordsDto GetUserReferralRecords { get; set; }
}

public class UserReferralRecordsDto
{
    public long TotalRecordCount { get; set; }
    public List<UserReferralDto> Data { get; set; }
}