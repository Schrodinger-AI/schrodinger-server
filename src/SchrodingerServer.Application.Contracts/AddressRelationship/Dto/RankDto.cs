using System;
using System.Collections.Generic;
using SchrodingerServer.Common.Options;

namespace SchrodingerServer.AddressRelationship.Dto;

public class RankDto
{
    public List<ActivityRankData> Data { get; set; }
    public List<RankHeader> Header { get; set; }
}

public class ActivityRankData
{
    public string Address { get; set; }
    public long Scores { get; set; }
    public string Reward { get; set; }
    
    public DateTime UpdateTime { get; set; }
}


public class RankItem
{
    public DateTime UpdateTime { get; set; }
    public long TotalAmount { get; set; }
}

