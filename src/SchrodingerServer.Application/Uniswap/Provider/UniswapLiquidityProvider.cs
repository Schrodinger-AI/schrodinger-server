using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using Volo.Abp.ObjectMapping;
using GraphQL;
using Nest;
using SchrodingerServer.Common.GraphQL;
using SchrodingerServer.Dtos.Uniswap;
using SchrodingerServer.Uniswap.Index;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Uniswap.Provider;

public interface IUniswapLiquidityProvider
{
    Task AddPositionsSnapshotAsync(List<UniswapPositionSnapshotIndex> positions);
    Task<GetUniswapLiquidityDto> GetPositionsSnapshotAsync(DateTime snapshotTime);
    Task<PoolsDto> GetPoolsAsync(string token0, int number);
    Task<PoolDayDatasDto> GetPoolDayDataAsync(string pool, long date);
    Task<PositionsDto> GetPositionsAsync(string poolId, int blockNo, int offset, int size);

    Task<PoolDto> GetPoolAsync(string pool, int number);

    double CalculateToken0Amount(string liquidity, double priceA, double priceB);
    double CalculateToken1Amount(string liquidity, double priceA, double priceB);
    
    Task<List<UniswapPositionSnapshotIndex>> GetAllSnapshotAsync(string bizDate);
}

public class UniswapLiquidityProvider : IUniswapLiquidityProvider, ISingletonDependency
{
    private readonly double Q96 = Math.Pow(2, 96);
    
    private readonly IGraphQLClientFactory _graphQlClientFactory;
    private readonly INESTRepository<UniswapPositionSnapshotIndex, string> _uniswapPositionSnapshotRepository;
    private readonly IObjectMapper _objectMapper;
    
    public UniswapLiquidityProvider(INESTRepository<UniswapPositionSnapshotIndex, string> uniswapPositionSnapshotRepository, 
        IGraphQLClientFactory graphQlClientFactory,
        IObjectMapper objectMapper)
    {
        _uniswapPositionSnapshotRepository = uniswapPositionSnapshotRepository;
        _graphQlClientFactory = graphQlClientFactory;
        _objectMapper = objectMapper;
    }

    public async Task AddPositionsSnapshotAsync(List<UniswapPositionSnapshotIndex> positions)
    {
        await _uniswapPositionSnapshotRepository.BulkAddOrUpdateAsync(positions);
    }
    
    public async Task<GetUniswapLiquidityDto> GetPositionsSnapshotAsync(DateTime snapshotTime)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<UniswapPositionSnapshotIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Terms(i => i.Field(f => f.BizDate).Terms(snapshotTime)));

        QueryContainer Filter(QueryContainerDescriptor<UniswapPositionSnapshotIndex> f) =>
            f.Bool(b => b.Must(mustQuery));
        var (totalCount, snapshotIndexes) = await _uniswapPositionSnapshotRepository.GetListAsync(Filter);

        return new GetUniswapLiquidityDto
        {
            TotalCount = totalCount,
            Items = _objectMapper.Map<List<UniswapPositionSnapshotIndex>, List<UniswapLiquidityDto>>(snapshotIndexes)
        };
    }
    

    public async Task<PoolsDto> GetPoolsAsync(string token0, int number)
    {
        var query = new GraphQLRequest
        {
            Query =
                @"query Pools($token0: String!, $number: Int!){
                    pools(where: {token0: $token0}, block:{number: $number}){
                        id
                        token0 {
                            symbol
                            id
                            decimals
                            derivedETH
                        }
                        token1 {
                            symbol
                            id
                            decimals
                            derivedETH
                        }
                    }
            }",
            Variables = new
            {
                token0 = token0,
                number = number
            }
        };
        var indexerResult = await _graphQlClientFactory.GetClient(GraphQLClientEnum.Uniswap).SendQueryAsync<PoolsDto>(query);

        return indexerResult.Data;
    }
    
    public async Task<PoolDto> GetPoolAsync(string pool, int number)
    {
        var query = new GraphQLRequest
        {
            Query =
                @"query Pool($id: String!, $number: Int!){
                    pool(id: $id, block:{number: $number}){
                      id
                      sqrtPrice  
                      tick
                      token0Price
                      token1Price
                      token0 {
                        symbol
                        id
                        decimals
                        }
                      token1 {
                        symbol
                        id
                        decimals
                        }        
                    }
            }",
            Variables = new
            {
                id = pool,
                number = number
            }
        };
        var indexerResult = await _graphQlClientFactory.GetClient(GraphQLClientEnum.Uniswap).SendQueryAsync<PoolDto>(query);

        return indexerResult.Data;
    }
    
    public async Task<PoolDayDatasDto> GetPoolDayDataAsync(string pool, long date)
    {
        var query = new GraphQLRequest
        {
            Query =
                @"query PoolDayDatas($pool: String!, $date: Int!){
                    poolDayDatas(first: 1, where: {pool: $pool, date_lte: $date}, orderBy:date, orderDirection:desc){  
                      high
                      low
                      token0Price
                      token1Price            
                      date
                      txCount  
                }
            }",
            Variables = new
            {
                pool = pool,
                date = date
            }
        };
        var indexerResult = await _graphQlClientFactory.GetClient(GraphQLClientEnum.Uniswap).SendQueryAsync<PoolDayDatasDto>(query);

        return indexerResult.Data;
    }
    
    public async Task<PositionsDto> GetPositionsAsync(string poolId, int blockNo, int offset, int size)
    {
        var query = new GraphQLRequest
        {
            Query =
                @"query Positions($pool: String!, $number: Int!, $skip: Int!, $first: Int!){
                    positions(skip: $skip, first: $first, block: {number: $number}, where: {pool: $pool}){
                      id
                      owner
                      liquidity
                      depositedToken0
                      depositedToken1
                      tickLower {
                        tickIdx
    	                price0
                        price1
                      }
                      tickUpper {
                        tickIdx
                        price0
                        price1
                      }
                }
            }",
            Variables = new
            {
                pool = poolId,
                number = blockNo,
                skip = offset,
                first = size
            }
        };
        
        var indexerResult = await _graphQlClientFactory.GetClient(GraphQLClientEnum.Uniswap).SendQueryAsync<PositionsDto>(query);

        return indexerResult.Data;
    }


    public double CalculateToken0Amount(string liquidity, double priceA, double priceB)
    {
        var sqrtPriceA = PriceToSqrtp(priceA);
        var sqrtPriceB = PriceToSqrtp(priceB);

        
        if (sqrtPriceA > sqrtPriceB)
        {
            (sqrtPriceA, sqrtPriceB) = (sqrtPriceB, sqrtPriceA);
        }

        return double.Parse(liquidity) * Q96 * (sqrtPriceB - sqrtPriceA) / sqrtPriceB / sqrtPriceA;
    }
    
    public double CalculateToken1Amount(string liquidity, double priceA, double priceB)
    {
        var sqrtPriceA = PriceToSqrtp(priceA);
        var sqrtPriceB = PriceToSqrtp(priceB);

        
        if (sqrtPriceA > sqrtPriceB)
        {
            (sqrtPriceA, sqrtPriceB) = (sqrtPriceB, sqrtPriceA);
        }
        
        return double.Parse(liquidity) / Q96 * (sqrtPriceB - sqrtPriceA);
    }

    private double PriceToSqrtp(double price)
    {
        return Math.Pow(price, 0.5) * Q96;
    }

    public async Task<List<UniswapPositionSnapshotIndex>> GetAllSnapshotAsync(string bizDate)
    {
        var res = new List<UniswapPositionSnapshotIndex>();
        List<UniswapPositionSnapshotIndex> list;
        var skipCount = 0;
        var mustQuery = new List<Func<QueryContainerDescriptor<UniswapPositionSnapshotIndex>, QueryContainer>>();
        
        mustQuery.Add(q => q.Term(i
            => i.Field(index => index.BizDate).Value(bizDate)));
        
        QueryContainer Filter(QueryContainerDescriptor<UniswapPositionSnapshotIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        do
        {
            list = (await _uniswapPositionSnapshotRepository.GetListAsync(filterFunc: Filter, skip: skipCount, limit: 10000)).Item2;
            var count = list.Count;
            res.AddRange(list);
            if (list.IsNullOrEmpty() || count < 10000)
            {
                break;
            }
            skipCount += count;
        } while (!list.IsNullOrEmpty());

        return res;
    }
}