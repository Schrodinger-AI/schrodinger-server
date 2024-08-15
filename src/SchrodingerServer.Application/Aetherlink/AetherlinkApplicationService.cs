using System.Threading.Tasks;
using Aetherlink.PriceServer;
using Aetherlink.PriceServer.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Services;

namespace SchrodingerServer.Aetherlink;

public class AetherlinkApplicationService : ApplicationService, IAetherlinkApplicationService
{
    private readonly ILogger<AetherlinkApplicationService> _logger;
    private readonly IPriceServerProvider _priceServerProvider;
    
    public AetherlinkApplicationService(ILogger<AetherlinkApplicationService> logger, IPriceServerProvider priceServerProvider)
    {
        _logger = logger;
        _priceServerProvider = priceServerProvider;
    }

    public async Task<decimal> GetTokenPriceInUsdt(string symbol)
    {
        var tokenPair = symbol.ToLower() + "-usdt";
        return (await _priceServerProvider.GetAggregatedTokenPriceAsync(new()
        {
            TokenPair = tokenPair,
            AggregateType = AggregateType.Latest
        })).Data.Price / 100000000;
    }
    
}