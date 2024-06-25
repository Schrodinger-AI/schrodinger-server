using System;
using System.Collections.Generic;

namespace SchrodingerServer.Dtos.Cat;

public class HoldingRankDto
{
    public List<RankItem> Items { get; set; }
}


public class RankItem
{
    public string Address { get; set; }
    public long Amount { get; set; }
    
    public DateTime UpdateTime { get; set; }
}


public  class RarityRankDto
{
    public List<RarityRankItem> Items { get; set; }
}


public class RarityRankItem
{
    public string Address { get; set; }
    public long Diamond { get; set; } = 0;
    public long Emerald { get; set; } = 0;
    public long Platinum { get; set; } = 0;
    public long Gold { get; set; } = 0;
    public long Silver { get; set; } = 0;
    public long Bronze { get; set; } = 0;
    
    public DateTime UpdateTime { get; set; }
}


public class HoldingRankQueryDto
{
    public List<RankItem> GetHoldingRank { get; set; }
}


public class RarityRankQueryDto
{
    public List<RarityRankItem> GetRarityRank { get; set; }
}

