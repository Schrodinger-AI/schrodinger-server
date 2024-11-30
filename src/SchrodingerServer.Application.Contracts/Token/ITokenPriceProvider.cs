using System.Threading.Tasks;

namespace SchrodingerServer.Token;

public interface ITokenPriceProvider
{
    Task<decimal> GetPriceByCacheAsync(string symbol);
}