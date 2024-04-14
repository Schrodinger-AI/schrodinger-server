using System.Threading.Tasks;
using SchrodingerServer.Point;

namespace SchrodingerServer.Token;

public interface IExchangeProvider
{
    public ExchangeProviderName Name();

    public Task<TokenExchangeDto> LatestAsync(string fromSymbol, string toSymbol);

}


public enum ExchangeProviderName
{
    GateIo
}