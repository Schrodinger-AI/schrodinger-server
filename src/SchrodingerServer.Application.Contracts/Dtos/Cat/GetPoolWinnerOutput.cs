using System.Collections.Generic;

namespace SchrodingerServer.Dtos.Cat;

public class GetPoolWinnerOutput
{
    public string WinnerAddress { get; set; }
    public string WinnerSymbol { get; set; }
    public string WinnerDescribe { get; set; }
    public bool IsOver { get; set; } 
    public List<PoolRankItem> RankList { get; set; } = new();
}

public class PoolRankItem
{
    public string Address { get; set; }
    public string Symbol { get; set; }
    public string Describe { get; set; }
}