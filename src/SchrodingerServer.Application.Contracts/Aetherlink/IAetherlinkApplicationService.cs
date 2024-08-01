using System.Threading.Tasks;

namespace SchrodingerServer.Aetherlink;

public interface IAetherlinkApplicationService
{
    Task<decimal> GetTokenPriceInUsdt(string symbol);
}