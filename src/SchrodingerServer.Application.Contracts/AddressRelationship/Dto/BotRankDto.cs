using System.Collections.Generic;
using SchrodingerServer.Common.Options;

namespace SchrodingerServer.AddressRelationship.Dto;

public class BotRankDto
{
    public List<ActivityRankData> Data { get; set; }
    public decimal MyScores { get; set; }
    public string MyReward { get; set; }
}