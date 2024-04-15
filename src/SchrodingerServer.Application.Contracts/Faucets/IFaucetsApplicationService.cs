using System.Threading.Tasks;
using SchrodingerServer.Dtos.Faucets;

namespace SchrodingerServer.Faucets;

public interface IFaucetsApplicationService
{
    public Task<FaucetsTransferResultDto> FaucetsTransferAsync(FaucetsTransferDto input);
}