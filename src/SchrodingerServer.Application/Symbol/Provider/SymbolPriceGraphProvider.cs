using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using Microsoft.Extensions.Logging;
using SchrodingerServer.Common;
using SchrodingerServer.Common.GraphQL;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Symbol.Provider;

public interface ISymbolPriceGraphProvider
{
    public Task<PagedResultDto<IndexerNFTListingInfo>> GetNFTListingsAsync(GetNFTListingsDto dto);
}

public class SymbolPriceGraphProvider: ISymbolPriceGraphProvider, ISingletonDependency
{
    private readonly IGraphQLClientFactory _graphQlClientFactory;
    private readonly ILogger<SymbolPriceGraphProvider> _logger;

    public SymbolPriceGraphProvider(IGraphQLClientFactory graphQlClientFactory,ILogger<SymbolPriceGraphProvider> logger)
    {
        _graphQlClientFactory = graphQlClientFactory;
        _logger = logger;
    }

    public async Task<PagedResultDto<IndexerNFTListingInfo>> GetNFTListingsAsync(GetNFTListingsDto dto){
        try
        {
            var res = await _graphQlClientFactory.GetClient(GraphQLClientEnum.ForestClient).SendQueryAsync<NFTListingPage>(new GraphQLRequest
            {
                Query = @"query (
                    $skipCount:Int!,
                    $maxResultCount:Int!,
                    $chainId:String,
                    $symbol:String
                ){
                  nftListingInfo(
                    input:{
                      skipCount:$skipCount,
                      maxResultCount:$maxResultCount,
                      chainId:$chainId,
                      symbol:$symbol
                    }
                  ){
                    TotalCount: totalRecordCount,
                    Message: message,
                    Items: data{
                      quantity,
                      realQuantity,
                      symbol,
                      owner,
                      prices,
                      startTime,
                      publicTime,
                      expireTime,
                      chainId,
                      purchaseToken {
      	                chainId,symbol,tokenName,
                      }
                    }
                  }
                }",
                Variables = new
                {
                    chainId = dto.ChainId, 
                    symbol = dto.Symbol, 
                    skipCount = dto.SkipCount, 
                    maxResultCount = dto.MaxResultCount,
                }
            });
            return res.Data?.nftListingInfo;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetNFTListingsAsync query GraphQL error");
            throw;
        }
    }

    
}

public class IndexerNFTListingInfo
{
    public string Id { get; set; }
    public long Quantity { get; set; }
    public string Symbol { get; set; }
    public string Owner { get; set; }
    public string ChainId { get; set; }
    public decimal Prices { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime PublicTime { get; set; }
    public DateTime ExpireTime { get; set; }
    public IndexerTokenInfo PurchaseToken { get; set; }
    public long RealQuantity { get; set; }

}

public class IndexerTokenInfo
{
    public string ChainId { get; set; }
    public string Address { get; set; }
    public string Symbol { get; set; }
}

public class NFTListingPage : IndexerCommonResult<NFTListingPage>
{
    public PagedResultDto<IndexerNFTListingInfo> nftListingInfo { get; set; }
    
}