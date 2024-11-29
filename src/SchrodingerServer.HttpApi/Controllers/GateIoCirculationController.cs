using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SchrodingerServer.GateIo;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace SchrodingerServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("GateIoCirculationController")]
[Route("api/app/circulation")]
public class GateIoCirculationController : AbpController
{
    private readonly IGateIoCirculationService _gateIoCirculationService;

    public GateIoCirculationController(IGateIoCirculationService gateIoCirculationService)
    {
        _gateIoCirculationService = gateIoCirculationService;
    }


    [HttpGet]
    public async Task<long> GetSgrCirculation()
    {
        return await _gateIoCirculationService.GetSgrCirculation();
    }
    
    
    [HttpGet("price")]
    public async Task<decimal> GetSgrPrice()
    {
        return await _gateIoCirculationService.GetSgrPrice();
    }
    
    [HttpGet("test")]
    public async Task<bool> DelCacheAsync(string key)
    {
        return await _gateIoCirculationService.DelCacheAsync(key);
    }
}