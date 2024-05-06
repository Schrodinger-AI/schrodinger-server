using System.Collections.Generic;

namespace SchrodingerServer.Dto;

public class PointsDetailDto
{
    public  string Id { get; set; }
    public string Address { get; set; }
    public string Domain { get; set; }
    public string DappId { get; set; }
    public string PointsName { get; set; }
    public decimal Amount { get; set; }
    public string SymbolName { get; set; }
    public string ActionName { get; set; }
}

public class PointsDetailIndexerQueryDto
{
    public PointsDetailIndexerListDto GetPointsRecordByName { get; set; }
}

public class PointsDetailIndexerListDto
{
    public List<PointsDetailDto> Data { get; set; }
    public long TotalRecordCount { get; set; }
}