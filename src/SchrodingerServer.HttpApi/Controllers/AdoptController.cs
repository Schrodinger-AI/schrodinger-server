using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SchrodingerServer.Adopts;
using SchrodingerServer.Common;
using SchrodingerServer.Dtos.Adopts;
using SchrodingerServer.Dtos.TraitsDto;
using SchrodingerServer.Image;
using SchrodingerServer.Traits;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace SchrodingerServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("adopt")]
[Route("api/app/schrodinger")]
public class AdoptController : AbpController
{

    private readonly IAdoptApplicationService _adoptApplicationService;
    
    public AdoptController(IAdoptApplicationService adoptApplicationService)
    {
        _adoptApplicationService = adoptApplicationService;
    }

    [HttpGet("imageInfo")]
    public async Task<GetAdoptImageInfoOutput?> GetAdoptImageInfoAsync(GetAdoptImageInfoInput input)
    {
        return await _adoptApplicationService.GetAdoptImageInfoAsync(input);
    }
    
    [HttpPost("waterMarkImageInfo")]
    public async Task<GetWaterMarkImageInfoOutput> GetWaterMarkImageInfoAsync(GetWaterMarkImageInfoInput input)
    {
        return await _adoptApplicationService.GetWaterMarkImageInfoAsync(input);
    }
    
    [HttpGet("isOverLoaded")]
    public async Task<bool> IsOverLoaded()
    {
        return await _adoptApplicationService.IsOverLoadedAsync();
    }
    
    [HttpGet("adoptInfo")]
    public async Task<ImageInfoForDirectAdoptionOutput?> GetAdoptImageInfoForDirectAdoptionAsync(GetAdoptImageInfoInput input)
    {
        return await _adoptApplicationService.GetAdoptImageInfoForDirectAdoptionAsync(input);
    }

    [HttpPost("confirm-adoption")]
    public async Task<ConfirmAdoptionOutput> ConfirmAdoptionAsync(ConfirmAdoptionInput input)
    {
        return await _adoptApplicationService.ConfirmAdoptionAsync(input);
    }
    
    [HttpGet("votes")]
    public async Task<GetVotesOutput> GetVoteAsync()
    {
        return await _adoptApplicationService.GetVoteAsync();
    }
}
