using System.Threading.Tasks;

namespace SchrodingerServer.Chains
{
    public interface IChainAppService
    {
        Task<string[]> GetListAsync();
        
        Task<string> GetChainIdAsync(int index);
        
    }
}