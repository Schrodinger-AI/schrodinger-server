using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SchrodingerServer.Cat;
using SchrodingerServer.Dtos.Cat;
using Volo.Abp;

namespace SchrodingerServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Cat")]
[Route("api/app/cat")]
public class SchrodingerCatController
{
    private readonly ISchrodingerCatService _schrodingerCatService;

    public SchrodingerCatController(ISchrodingerCatService schrodingerCatService)
    {
        _schrodingerCatService = schrodingerCatService;
    }

    [HttpPost("list")]
    public async Task<SchrodingerListDto> GetSchrodingerCatList(GetCatListInput input)
    {
        return await _schrodingerCatService.GetSchrodingerCatListAsync(input);
    }
    
}