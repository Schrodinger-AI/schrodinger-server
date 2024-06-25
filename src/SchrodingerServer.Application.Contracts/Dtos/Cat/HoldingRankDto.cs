using System;
using System.Collections.Generic;

namespace SchrodingerServer.Dtos.Cat;

public class HoldingRankDto
{
    public List<RankItemDto> Items { get; set; }
}


public class RankItem
{
    public string Address { get; set; }
    public decimal Amount { get; set; }
    
    public DateTime UpdateTime { get; set; }
}

public class RankItemDto
{
    public string Address { get; set; }
    public decimal Amount { get; set; }
}


public  class RarityRankDto
{
    public List<RarityRankItemDto> Items { get; set; }
}


public class RarityRankItem
{
    public string Address { get; set; }
    public decimal Diamond { get; set; } = 0;
    public decimal Emerald { get; set; } = 0;
    public decimal Platinum { get; set; } = 0;
    public decimal Gold { get; set; } = 0;
    public decimal Silver { get; set; } = 0;
    public decimal Bronze { get; set; } = 0;
    
    public DateTime UpdateTime { get; set; }
}

public class RarityRankItemDto
{
    public string Address { get; set; }
    public decimal Diamond { get; set; } = 0;
    public decimal Emerald { get; set; } = 0;
    public decimal Platinum { get; set; } = 0;
    public decimal Gold { get; set; } = 0;
    public decimal Silver { get; set; } = 0;
    public decimal Bronze { get; set; } = 0;
}


public class HoldingRankQueryDto
{
    public List<RankItem> GetHoldingRank { get; set; }
}


public class RarityRankQueryDto
{
    public List<RarityRankItem> GetRarityRank { get; set; }
}

