using System.Threading.Tasks;
using SchrodingerServer.Dtos.Adopts;

namespace SchrodingerServer.Adopts;

public interface IAdoptApplicationService
{
    Task<GetAdoptImageInfoOutput> GetAdoptImageInfoAsync(string adoptId);

    Task<GetWaterMarkImageInfoOutput> GetWaterMarkImageInfoAsync(GetWaterMarkImageInfoInput input);
    Task<bool> IsOverLoadedAsync();
}