using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SchrodingerServer.Aetherlink;
using SchrodingerServer.Fee;
using SchrodingerServer.Token;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace SchrodingerServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Token")]
[Route("api/app")]
public class TokenController : AbpController
{
    private readonly ITokenPriceProvider _tokenPriceProvider;
    private readonly ITransactionFeeAppService _transactionFeeAppService;
    private readonly IAetherlinkApplicationService _aetherlinkApplicationService;
    private const string UsdSymbol = "usd";

    public TokenController(ITokenPriceProvider tokenPriceProvider, ITransactionFeeAppService transactionFeeAppService, IAetherlinkApplicationService aetherlinkApplicationService)
    {
        _tokenPriceProvider = tokenPriceProvider;
        _transactionFeeAppService = transactionFeeAppService;
        _aetherlinkApplicationService = aetherlinkApplicationService;
    }

    [HttpGet("schrodinger/transaction-fee")]
    public TransactionFeeResultDto CalculateFee()
    {
        return _transactionFeeAppService.CalculateFee();
    }

    [HttpGet("schrodinger/token-price")]
    public async Task<PriceDto> GetPriceAsync(GetTokenPriceInput input)
    {
        var price = await _tokenPriceProvider.GetPriceByCacheAsync(input.Symbol);
        return new PriceDto() { Price = price };
    }
    
    [HttpGet("schrodinger/test-price/{symbol}")]
    public async Task<decimal> AetherlinkPirce(string symbol)
    {
        return await _aetherlinkApplicationService.GetTokenPriceInUsdt(symbol);
    }
    
}