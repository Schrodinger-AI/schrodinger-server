using System.Threading.Tasks;
using SchrodingerServer.Dtos.Cat;

namespace SchrodingerServer.Cat;

public interface ISchrodingerCatService
{
    Task<SchrodingerListDto> GetSchrodingerCatListAsync(GetCatListInput input);
    
    Task<SchrodingerListDto> GetSchrodingerAllCatsListAsync(GetCatListInput input);
    
    Task<SchrodingerDetailDto> GetSchrodingerCatDeailAsync (GetCatDetailInput input);

}