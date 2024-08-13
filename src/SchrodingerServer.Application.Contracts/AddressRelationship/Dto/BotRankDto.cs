using System.Collections.Generic;
using SchrodingerServer.Common.Options;

namespace SchrodingerServer.AddressRelationship.Dto;

public class BotRankDto
{
    public List<ActivityRankData> Data { get; set; }
    public decimal MyScore { get; set; }
    public decimal MyReward { get; set; }
    public int MyRank { get; set; }
}