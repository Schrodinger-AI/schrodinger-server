using System.Collections.Generic;

namespace SchrodingerServer.Background.Dtos;

public class RankingDetailIndexerQueryDto
{
    public RankingDetailIndexerListDto GetPointsSumByAction { get; set; }
}

public class RankingDetailIndexerListDto
{
    public List<RankingDetailIndexerDto> Data { get; set; }
    public long TotalRecordCount { get; set; }
}

public class RankingDetailIndexerDto
{
    public string Address { get; set; }
    public string Domain { get; set; }
    public string DappId { get; set; }
    public string PointsName { get; set; }
    public decimal Amount { get; set; }
    public string SymbolName { get; set; }
}