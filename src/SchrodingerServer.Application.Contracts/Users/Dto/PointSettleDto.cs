using System.Collections.Generic;
using System.Linq;
using Orleans;
using SchrodingerServer.Users.Index;

namespace SchrodingerServer.Users.Dto;

[GenerateSerializer]
public class PointSettleDto
{
    [Id(0)]
    public string ChainId { get; set; }
    [Id(1)]
    public string BizId { get; set; }

    [Id(2)]
    public string PointName { get; set; }

    [Id(3)]
    public List<UserPointInfo> UserPointsInfos { get; set; }

    public static PointSettleDto Of(string chainId, string pointName, string bizId, List<PointDailyRecordIndex> tradeList)
    {
        return new PointSettleDto
        {
            ChainId = chainId,
            PointName = pointName,
            BizId = bizId,
            UserPointsInfos = tradeList.Select(item => new UserPointInfo
            {
                Id = item.Id,
                Address = item.Address,
                PointAmount = item.PointAmount
            }).ToList()
        };
    }
}

[GenerateSerializer]
public class UserPointInfo
{
    [Id(0)]
    public string Id { get; set; }

    [Id(1)]
    public string Address { get; set; }

    [Id(2)]
    public decimal PointAmount { get; set; }
}