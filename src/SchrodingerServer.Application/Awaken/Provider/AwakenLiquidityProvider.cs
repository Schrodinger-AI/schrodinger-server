using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Dtos;
using SchrodingerServer.Common.GraphQL;
using SchrodingerServer.Common.HttpClient;
using SchrodingerServer.Options;
using SchrodingerServer.PointServer;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Awaken.Provider;

public interface IAwakenLiquidityProvider
{
    Task<List<AwakenLiquidityRecordDto>> GetLiquidityRecordsAsync(GetAwakenLiquidityRecordDto dto);
    Task<GetAwakenPriceDto> GetPriceAsync(string token0Symbol, string token1Symbol, string chainId, string feeRate);
}

public class AwakenLiquidityProvider : IAwakenLiquidityProvider, ISingletonDependency
{
    private readonly IGraphQLClientFactory _graphQlClientFactory;
    private readonly ILogger<AwakenLiquidityProvider> _logger;
    private readonly IHttpProvider _httpProvider;
    private readonly IOptionsMonitor<LevelOptions> _levelOptions;

    public AwakenLiquidityProvider( 
        IGraphQLClientFactory graphQlClientFactory,
        IHttpProvider httpProvider,
        IOptionsMonitor<LevelOptions> levelOptions,
        ILogger<AwakenLiquidityProvider> logger)
    {
        _graphQlClientFactory = graphQlClientFactory;
        _logger = logger;
        _httpProvider = httpProvider;
        _levelOptions = levelOptions;
    }
    

    public async Task<List<AwakenLiquidityRecordDto>> GetLiquidityRecordsAsync(GetAwakenLiquidityRecordDto dto)
    {
        try
        {
            var res = await _graphQlClientFactory.GetClient(GraphQLClientEnum.AwakenClient).SendQueryAsync<GetAwakenLiquidityRecordResultDto>(new GraphQLRequest
            {
                Query = @"query (
                    $skipCount:Int!,
                    $maxResultCount:Int!,
                    $chainId:String!,
                    $pair:String!,
                    $timestampMax:Long!,
                    $timestampMin:Long!,
                ){
                  liquidityRecord(
                    dto:{
                      skipCount:$skipCount,
                      maxResultCount:$maxResultCount,
                      chainId:$chainId,
                      pair:$pair,
                      timestampMax:$timestampMax,
                      timestampMin:$timestampMin
                    }
                  ){
                    totalCount,
                    data{
                        chainId,
                        pair,
                        to,
                        address,
                        token0Amount,
                        token1Amount,
                        token0,
                        token1,
                        lpTokenAmount,
                        transactionHash,
                        channel,
                        sender,
                        type,
                        timestamp
                    }
                  }
                }",
                Variables = new
                {
                    chainId = dto.ChainId, 
                    pair = dto.Pair, 
                    skipCount = dto.SkipCount, 
                    maxResultCount = dto.MaxResultCount,
                    timestampMax = dto.TimestampMax,
                    timestampMin = dto.TimestampMin
                }
            });
            return res.Data.LiquidityRecord.Data;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetNFTListingsAsync query GraphQL error");
            throw;
        }
    }
    
    
    public async Task<GetAwakenPriceDto> GetPriceAsync(string token0Symbol, string token1Symbol, string chainId, string feeRate)
    {
        try
        {
            var resp = await _httpProvider.InvokeAsync<CommonResponseDto<GetAwakenPriceDto>>(
                _levelOptions.CurrentValue.AwakenUrl, PointServerProvider.Api.GetAwakenPrice, null,
                new Dictionary<string, string>()
                {
                    ["token0Symbol"] = token0Symbol,
                    ["token1Symbol"] = token1Symbol,
                    ["feeRate"] = feeRate,
                    ["chainId"] = chainId
                });
            AssertHelper.NotNull(resp, "Response empty");
            AssertHelper.NotNull(resp.Success, "Response failed, {}", resp.Message);
            return resp.Data ?? new GetAwakenPriceDto();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Points domain get points failed");
            return new GetAwakenPriceDto();
        }
    }
}

public class AwakenLiquidityRecordDto
{
    public string ChainId { get; set; }
    public string Address { get; set; }
    public string Token0 { get; set; }
    public string Token1 { get; set; }
    public long Token0Amount { get; set; }
    public long Token1Amount { get; set; }
    public string TransactionHash { get; set; }
    public string Type { get; set; }
    public long Timestamp { get; set; }
}

public class AwakenLiquidityRecordListDto
{
    public long TotalCount { get; set; }
    public List<AwakenLiquidityRecordDto> Data { get; set; }
}

public class GetAwakenLiquidityRecordResultDto
{
    public AwakenLiquidityRecordListDto LiquidityRecord { get; set; }
}

public class GetAwakenLiquidityRecordDto 
{
    public string ChainId { get; set; }
    public string Pair { get; set; }
    public string Type { get; set; }
    public long TimestampMin { get; set; }
    public long TimestampMax { get; set; }
    public int SkipCount { get; set; }
    
    public int MaxResultCount { get; set; }
}

public class GetAwakenPriceDto 
{
    public int TotalCount { get; set; }
    public List<GetAwakenPriceDetail> Items { get; set; } = new();
}



public class GetAwakenPriceDetail 
{
    public decimal Price { get; set; }
    public decimal priceUSD { get; set; }
    
}




