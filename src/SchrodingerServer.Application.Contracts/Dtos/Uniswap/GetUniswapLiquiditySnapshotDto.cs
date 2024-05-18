using System;
using Volo.Abp.Application.Dtos;

namespace SchrodingerServer.Dtos.Uniswap;

public class GetUniswapLiquidityDto : PagedResultDto<UniswapLiquidityDto>
{
    
}

public class UniswapLiquidityDto
{
    public string Id { get; set; }
    public string PositionOwner { get; set; }
    public string PositionId { get; set; }

    public double PositionValueUSD { get; set; }
    
    public DateTime SnapshotTime { get; set; }
    public DateTime CreateTime { get; set; }
}