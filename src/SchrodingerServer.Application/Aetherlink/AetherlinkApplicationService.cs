using System;
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
        try
        {
            var tokenPair = symbol.ToLower() + "-usdt";
            var price = (await _priceServerProvider.GetAggregatedTokenPriceAsync(new()
            {
                TokenPair = tokenPair,
                AggregateType = AggregateType.Avg
            })).Data.Price;
            return price / (decimal)100000000;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetTokenPriceInUsdt error");
            return 0;
        }
    }
    
    public async Task<decimal> GetTokenPriceInElf(string symbol)
    {
        try
        {
            var tokenPair = symbol.ToLower() + "-elf";
            var price=  (await _priceServerProvider.GetAggregatedTokenPriceAsync(new()
            {
                TokenPair = tokenPair,
                AggregateType = AggregateType.Avg
            })).Data.Price;
            return price / (decimal)100000000;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetTokenPriceInElf error");
            return 0;
        }
    }
    
}