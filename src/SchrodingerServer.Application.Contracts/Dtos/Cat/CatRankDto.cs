namespace SchrodingerServer.Dtos.Cat;

public class CatRankDto
{
    public string Symbol { get; set; }
    public string TokenName { get; set; }
    public string InscriptionImageUri { get; set; }
    public long Amount { get; set; }
    public int Generation { get; set; }
    public int Rank { get; set; }
    public string Level { get; set; }
    public string Grade { get; set; }
    public string Star { get; set; }
    public string Rarity { get; set; }
}

public class SchrodingerCatQueryDto
{
    public CatRankDto GetSchrodingerRank { get; set; }
}