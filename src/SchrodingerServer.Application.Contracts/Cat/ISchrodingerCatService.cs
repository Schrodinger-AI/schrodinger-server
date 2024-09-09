using System.Threading.Tasks;
using SchrodingerServer.Dtos.Cat;

namespace SchrodingerServer.Cat;

public interface ISchrodingerCatService
{
    Task<SchrodingerListDto> GetSchrodingerCatListAsync(GetCatListInput input);
    
    Task<SchrodingerListDto> GetSchrodingerAllCatsListAsync(GetCatListInput input);
    
    Task<SchrodingerDetailDto> GetSchrodingerCatDetailAsync (GetCatDetailInput input);
    
    Task<HoldingRankDto> GetHoldingRankAsync ();

    Task<RarityRankDto> GetRarityRankAsync ();
    
    Task<SchrodingerListDto> GetSchrodingerCatListInBotAsync(GetCatListInput input);
    
    Task<SchrodingerListDto> GetSchrodingerAllCatsListInBotAsync(GetCatListInput input);
    
    Task<SchrodingerBoxListDto> GetSchrodingerBoxListAsync(GetBlindBoxListInput input);
    
    Task<BlindBoxDetailDto> GetSchrodingerBoxDetailAsync (GetCatDetailInput input);
}