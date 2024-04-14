using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SchrodingerServer.Symbol;
using SchrodingerServer.Token;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace SchrodingerServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Test")]
[Route("api/app/test")]
public class TestController : AbpControllerBase
{
    private readonly UniswapV3Provider _uniSwapV3Provider;
    private readonly IXgrPriceService _xgrPriceService;

    public TestController(UniswapV3Provider uniSwapV3Provider,IXgrPriceService xgrPriceService)
    {
        _uniSwapV3Provider = uniSwapV3Provider;
        _xgrPriceService = xgrPriceService;
    }
    
    [HttpGet("token")]
    public async Task<UniswapV3Provider.TokenResponse> GetLatestUSDPriceAsync(long date)
    {
        return await _uniSwapV3Provider.GetLatestUSDPriceAsync(date);
    }
    
    [HttpGet("saveXgrDayPrice")]
    public async Task SaveXgrDayPriceAsync(bool isGen0)
    {
         await _xgrPriceService.SaveXgrDayPriceAsync(isGen0);
    }
    
}