using System.Threading.Tasks;
using SchrodingerServer.Dtos.Adopts;

namespace SchrodingerServer.Adopts;

public interface IAdoptApplicationService
{
    Task<GetAdoptImageInfoOutput> GetAdoptImageInfoAsync(GetAdoptImageInfoInput input);
    Task<GetWaterMarkImageInfoOutput> GetWaterMarkImageInfoAsync(GetWaterMarkImageInfoInput input);
    Task<bool> IsOverLoadedAsync();
    Task<ImageInfoForDirectAdoptionOutput> GetAdoptImageInfoForDirectAdoptionAsync(GetAdoptImageInfoInput input);
    Task<ConfirmAdoptionOutput> ConfirmAdoptionAsync(ConfirmAdoptionInput input);
    Task<GetVotesOutput> GetVoteAsync();
    
}