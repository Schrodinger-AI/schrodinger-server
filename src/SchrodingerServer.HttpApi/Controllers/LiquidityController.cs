using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SchrodingerServer.Dtos.Uniswap;
using SchrodingerServer.Uniswap;
using Volo.Abp;

namespace SchrodingerServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Liquidity")]
[Route("api/app/liquidity")]
public class LiquidityController
{
    private readonly IUniswapLiquidityService _uniswapLiquidityService;

    public LiquidityController(IUniswapLiquidityService uniswapLiquidityService)
    {
        _uniswapLiquidityService = uniswapLiquidityService;
    }

    [HttpGet("uniswapSnapshot")]
    public async Task<GetUniswapLiquidityDto> GetUniswapLiquidity(GetUniswapLiquidityInput input)
        => await _uniswapLiquidityService.GetSnapshotAsync(input);
}