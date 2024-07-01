using System.Threading.Tasks;
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
    
    
    [HttpGet]
    public async Task<decimal> GetSgrPrice()
    {
        return await _gateIoCirculationService.GetSgrPrice();
    }
}