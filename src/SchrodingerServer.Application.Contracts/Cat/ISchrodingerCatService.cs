using System.Threading.Tasks;
using SchrodingerServer.Dtos.Cat;

namespace SchrodingerServer.Cat;

public interface ISchrodingerCatService
{
    Task<SchrodingerListDto> GetSchrodingerCatListAsync(GetCatListInput input);
}