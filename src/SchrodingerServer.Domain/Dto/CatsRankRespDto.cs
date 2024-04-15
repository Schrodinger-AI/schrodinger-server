using System.Collections.Generic;

namespace SchrodingerServer.Dto;

public class CatsRankRespDto
{
    public string Code { get; set; }
    public string Message { get; set; }
    public List<RankData> Data { get; set; }
}
public class RankGenOne
{
    public int Rank { get; set; }
    public string Total { get; set; }
    public string Probability { get; set; }
    public string Percent { get; set; }
    public Dictionary<string, double> TraitsProbability { get; set; }
    public List<string> ProbabilityTypes { get; set; }
}

public class Ranks
{
    public int Rank { get; set; }
    public string Total { get; set; }
    public string Probability { get; set; }
    public string Percent { get; set; }
    public string Type { get; set; }
    public Prices Price { get; set; }
}

public class RankTwoToNine
{
    public int Rank { get; set; }
    public int Total { get; set; }
    public string Probability { get; set; }
    public string Percent { get; set; }
    public Dictionary<string, double> TraitsProbability { get; set; }
    public List<string> ProbabilityTypes { get; set; }
}

public class Prices
{
    public string Elf { get; set; }
    public string Usd { get; set; }
    public string Sgr { get; set; }
}

public class RankData
{
    public RankGenOne RankGenOne { get; set; }
    public Ranks Rank { get; set; }
    public RankTwoToNine RankTwoToNine { get; set; }
    public LevelInfoDto LevelInfo { get; set; }
        
}