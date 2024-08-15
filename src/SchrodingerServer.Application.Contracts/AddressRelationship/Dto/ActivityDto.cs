using System.Collections.Generic;
using SchrodingerServer.Users.Index;

namespace SchrodingerServer.AddressRelationship.Dto;

public class ActivityDto
{
    public string ActivityId { get; set; }
    public string ActivityName { get; set; }
    public string BannerUrl { get; set; }
    public string LinkUrl { get; set; }
    public string LinkType { get; set; }
    public long BeginTime { get; set; }
    public long EndTime { get; set; }
    public bool IsNew { get; set; }
    public string TimeDescription { get; set; }
    public bool IsShow { get; set; }
}


public class ActivityListDto
{
    public List<ActivityDto> Items { get; set; }
    public long TotalCount { get; set; }
}