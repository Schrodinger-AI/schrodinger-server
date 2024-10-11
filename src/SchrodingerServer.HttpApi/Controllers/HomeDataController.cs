using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SchrodingerServer.Cat.Provider;
using SchrodingerServer.Dtos.Cat;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace SchrodingerServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Home")]
[Route("api/app")]
public class HomeDataController : AbpController
{
    private readonly ISchrodingerCatProvider _schrodingerCatProvider;

    public HomeDataController(ISchrodingerCatProvider schrodingerCatProvider)
    {
       _schrodingerCatProvider = schrodingerCatProvider;
    }

    [HttpGet("home")]
    public async Task<HomeDataDto> GetHomeData(GetHomeDataInput input)
    {
        var res = await _schrodingerCatProvider.GetHomeDataAsync(input.ChainId);
        return res;
    }
}