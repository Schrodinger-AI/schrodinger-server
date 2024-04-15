using System.Collections.Generic;
using System.Linq;
using SchrodingerServer.Users.Index;

namespace SchrodingerServer.Users.Dto;

public class PointSettleDto
{
    public string ChainId { get; set; }
    public string BizId { get; set; }

    public string PointName { get; set; }

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

public class UserPointInfo
{
    public string Id { get; set; }

    public string Address { get; set; }

    public decimal PointAmount { get; set; }
}