using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using GraphQL;
using Microsoft.Extensions.Logging;
using Nest;
using SchrodingerServer.Common.GraphQL;
using SchrodingerServer.Symbol;
using SchrodingerServer.Symbol.Provider;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Awaken.Provider;

public interface IAwakenLiquidityProvider
{
    Task<List<AwakenLiquidityRecordDto>> GetLiquidityRecordsAsync(GetAwakenLiquidityRecordDto dto);
}

public class AwakenLiquidityProvider : IAwakenLiquidityProvider, ISingletonDependency
{
    private readonly IGraphQLClientFactory _graphQlClientFactory;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<AwakenLiquidityProvider> _logger;

    public AwakenLiquidityProvider( 
        IGraphQLClientFactory graphQlClientFactory,
        IObjectMapper objectMapper,
        ILogger<AwakenLiquidityProvider> logger)
    {
        _graphQlClientFactory = graphQlClientFactory;
        _objectMapper = objectMapper;
        _logger = logger;
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


