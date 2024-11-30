using System.Threading.Tasks;

namespace SchrodingerServer.Token;

public interface ITokenPriceProvider
{
    Task<double> GetPriceByCacheAsync(string symbol);
}