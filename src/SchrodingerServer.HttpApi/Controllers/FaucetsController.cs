using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SchrodingerServer.Dtos.Faucets;
using SchrodingerServer.Faucets;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace SchrodingerServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Faucets")]
[Route("api/app/faucets")]
public class FaucetsController : AbpControllerBase
{
    private readonly IFaucetsApplicationService _faucetsApplicationService;

    public FaucetsController(IFaucetsApplicationService faucetsApplicationService)
    {
        _faucetsApplicationService = faucetsApplicationService;
    }

    [HttpPost("transfer")]
    public async Task<FaucetsTransferResultDto> FaucetsTransferAsync(FaucetsTransferDto input)
        => await _faucetsApplicationService.FaucetsTransferAsync(input);
}