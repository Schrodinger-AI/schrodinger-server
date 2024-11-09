using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SchrodingerServer.Cat;
using SchrodingerServer.Dto;
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
    
    [HttpPost("all")]
    public async Task<SchrodingerListDto> GetSchrodingerAllCatsList(GetCatListInput input)
    {
        return await _schrodingerCatService.GetSchrodingerAllCatsListAsync(input);
    }
    
    [HttpPost("detail")]
    public async Task<SchrodingerDetailDto> GetSchrodingerAllCatsList(GetCatDetailInput input)
    {
        return await _schrodingerCatService.GetSchrodingerCatDetailAsync(input);
    }
    
    [HttpGet("holding-rank")]
    public async Task<HoldingRankDto> GetHoldingRank()
    {
        return await _schrodingerCatService.GetHoldingRankAsync();
    }
    
    [HttpGet("rarity-rank")]
    public async Task<RarityRankDto> GetSchrodingerAllCatsList()
    {
        return await _schrodingerCatService.GetRarityRankAsync();
    }
    
    [HttpPost("bot-list")]
    public async Task<SchrodingerListDto> GetSchrodingerCatListInBot(GetCatListInput input)
    {
        return await _schrodingerCatService.GetSchrodingerCatListInBotAsync(input);
    }
    
    [HttpPost("bot-all")]
    public async Task<SchrodingerListDto> GetSchrodingerAllCatsListInBot(GetCatListInput input)
    {
        return await _schrodingerCatService.GetSchrodingerAllCatsListInBotAsync(input);
    }
    
    [HttpPost("box-list")]
    public async Task<SchrodingerBoxListDto> GetSchrodingerBoxList(GetBlindBoxListInput input)
    {
        return await _schrodingerCatService.GetSchrodingerBoxListAsync(input);
    }
    
    [HttpPost("box-detail")]
    public async Task<BlindBoxDetailDto> GetSchrodingerBoxList(GetCatDetailInput input)
    {
        return await _schrodingerCatService.GetSchrodingerBoxDetailAsync(input);
    }
    
    [HttpPost("stray-cats")]
    public async Task<StrayCatsListDto> GetStrayCatsAsync(StrayCatsInput input)
    {
        return await _schrodingerCatService.GetStrayCatsAsync(input);
    }
    
    [HttpPost("rarity")]
    public async Task<RankData> GetRarityAsync(GetRarityAsync input)
    {
        return await _schrodingerCatService.GetRarityAsync(input);
    }
    
    [HttpPost("combine")]
    public async Task<CombineOutput> CombineAsync(CombineInput input)
    {
        return await _schrodingerCatService.CombineAsync(input);
    }
}