using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using Microsoft.Extensions.Logging;
using SchrodingerServer.Cat.Provider.Dtos;
using SchrodingerServer.Common.GraphQL;
using SchrodingerServer.Dtos.Cat;
using SchrodingerServer.Message.Dtos;
using SchrodingerServer.Message.Provider.Dto;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Cat.Provider;

public interface ISchrodingerCatProvider
{
    Task<SchrodingerIndexerListDto> GetSchrodingerCatListAsync(GetCatListInput input);
    Task<SchrodingerSymbolIndexerListDto> GetSchrodingerAllCatsListAsync(GetCatListInput input);

    Task<SchrodingerDetailDto> GetSchrodingerCatDetailAsync(GetCatDetailInput input);
    
    Task<CatRankDto> GetSchrodingerCatRankAsync(GetCatRankInput input);
    
    Task<List<RankItem>> GetHoldingRankAsync();
    
    Task<List<RarityRankItem>> GetRarityRankAsync();
    
    Task<HomeDataDto> GetHomeDataAsync(string chainId);

    Task<List<NFTActivityIndexDto>> GetSchrodingerSoldListAsync(GetSchrodingerSoldInput input);

    Task<HoldingPointBySymbolDto> GetHoldingPointBySymbolAsync(string symbol, string chainId);
    
    Task<SchrodingerIndexerListDto> GetSchrodingerHoldingListAsync(GetCatListInput input);
}

public class SchrodingerCatProvider : ISchrodingerCatProvider, ISingletonDependency
{
    private readonly IGraphQlHelper _graphQlHelper;
    private readonly ILogger<SchrodingerCatProvider> _logger;

    public SchrodingerCatProvider(IGraphQlHelper graphQlHelper, ILogger<SchrodingerCatProvider> logger)
    {
        _graphQlHelper = graphQlHelper;
        _logger = logger;
    }


    public async Task<SchrodingerIndexerListDto> GetSchrodingerCatListAsync(GetCatListInput input)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<SchrodingerIndexerQuery>(new GraphQLRequest
            {
                Query =
                    @"query($keyword:String!, $chainId:String!, $address:String!, $tick:String!, $traits:[TraitInput!],$generations:[Int!],$skipCount:Int!,$maxResultCount:Int!,$filterSgr:Boolean!){
                    getSchrodingerList(input: {keyword:$keyword,chainId:$chainId,address:$address,tick:$tick,traits:$traits,generations:$generations,skipCount:$skipCount,maxResultCount:$maxResultCount,filterSgr:$filterSgr}){
                        totalCount,
                        data{
                        symbol,
                        tokenName,
                        inscriptionImageUri,
                        amount,
                        generation,
                        decimals,
                        inscriptionDeploy,
                        adopter,
                        adoptTime,
                        traits{traitType,value},
                        address
                    }
                }
            }",
                Variables = new
                {
                    keyword = input.Keyword ?? "", chainId = input.ChainId ?? "", address = input.Address ?? "",
                    tick = input.Tick ?? "", traits = input.Traits, generations = input.Generations,
                    skipCount = input.SkipCount, maxResultCount = input.MaxResultCount,filterSgr = input.FilterSgr
                }
            });

            return indexerResult.GetSchrodingerList;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetSchrodingerCatList Indexer error");
            return new SchrodingerIndexerListDto();
        }
    }

    public async Task<SchrodingerSymbolIndexerListDto> GetSchrodingerAllCatsListAsync(GetCatListInput input)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<SchrodingerSymbolIndexerQuery>(new GraphQLRequest
            {
                Query =
                    @"query($keyword:String!, $chainId:String!, $tick:String!, $traits:[TraitsInput!],$raritys:[String!],$generations:[Int!],$skipCount:Int!,$maxResultCount:Int!,$filterSgr:Boolean!){
                    getAllSchrodingerList(input: {keyword:$keyword,chainId:$chainId,tick:$tick,traits:$traits,raritys:$raritys,generations:$generations,skipCount:$skipCount,maxResultCount:$maxResultCount,filterSgr:$filterSgr}){
                        totalCount,
                        data{
                        symbol,
                        tokenName,
                        inscriptionImageUri,
                        amount,
                        generation,
                        decimals,
                        inscriptionDeploy,
                        adopter,
                        adoptTime,
                        traits{traitType,value},
                        rarity,
                        rank,
                        level,
                        grade
                    }
                }
            }",
                Variables = new
                {
                    keyword = input.Keyword ?? "", chainId = input.ChainId ?? "",
                    tick = input.Tick ?? "", traits = input.Traits,raritys = input.Rarities, generations = input.Generations,
                    skipCount = input.SkipCount, maxResultCount = input.MaxResultCount,filterSgr = input.FilterSgr
                }
            });

            return indexerResult.GetAllSchrodingerList;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetSchrodingerAllCatsListAsync Indexer error");
            return new SchrodingerSymbolIndexerListDto();
        }
    }

    public async Task<SchrodingerDetailDto> GetSchrodingerCatDetailAsync(GetCatDetailInput input)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<SchrodingerDetailQueryDto>(new GraphQLRequest
            {
                Query =
                    @"query($chainId:String!, $address:String!, $symbol:String!){
                    getSchrodingerDetail(input: {chainId:$chainId,address:$address,symbol:$symbol}){
                        symbol,
                        tokenName,
                        inscriptionImageUri,
                        amount,
                        generation,
                        address,
                        decimals
                        traits{traitType,value,percent,isRare}
                    
                }
            }",
                Variables = new
                {
                    chainId = input.ChainId ?? "",
                    address = input.Address ?? "",
                    symbol = input.Symbol ?? ""
                }
            });

            return indexerResult.GetSchrodingerDetail;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetSchrodingerAllCatsListAsync Indexer error");
            return new SchrodingerDetailDto();
        }
    }

    public async Task<CatRankDto> GetSchrodingerCatRankAsync(GetCatRankInput input)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<SchrodingerCatQueryDto>(new GraphQLRequest
            {
                Query =
                    @"query($chainId:String!, $symbol:String!){
                    getSchrodingerRank(input: {chainId:$chainId, symbol:$symbol}){
                        symbol,
                        tokenName,
                        inscriptionImageUri,
                        amount,
                        generation,
                        grade,
                        rank,
                        rarity,
                        star,
                        level
                }
            }",
                Variables = new
                {
                    chainId = input.ChainId ?? "",
                    symbol = input.Symbol ?? ""
                }
            });

            return indexerResult.GetSchrodingerRank;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "getSchrodingerRank error");
            return new CatRankDto();
        }
    }

    public async Task<List<RankItem>> GetHoldingRankAsync()
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<HoldingRankQueryDto>(new GraphQLRequest
            {
                Query =
                    @"query($rankNumber:Int!){
                    getHoldingRank(input: {rankNumber:$rankNumber}){
                        address,
                        amount        
                }
            }",
                Variables = new
                {
                    rankNumber = 100
                }
            });

            return indexerResult.GetHoldingRank;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "getHoldingRank error");
            return new List<RankItem>();
        }
    }
    
    public async Task<List<RarityRankItem>> GetRarityRankAsync()
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<RarityRankQueryDto>(new GraphQLRequest
            {
                Query =
                    @"query($rankNumber:Int!){
                    getRarityRank(input: {rankNumber:$rankNumber}){
                        address,
        			    diamond,
        			    emerald,
        			    platinum,
        			    gold,
        			    silver,
        			    bronze
                }
            }",
                Variables = new
                {
                    rankNumber = 100
                }
            });

            return indexerResult.GetRarityRank;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "getRarityRank error");
            return new List<RarityRankItem>();
        }
    }

    public async Task<HomeDataDto> GetHomeDataAsync(string chainId)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<HomeDataQueryDto>(new GraphQLRequest
            {
                Query =
                    @"query($chainId:String!){
                    getHomeData(input: {chainId:$chainId}){
                        symbolCount,
                        holdingCount,
        		        tradeVolume
                }
            }",
                Variables = new
                {
                    chainId = chainId
                }
            });

            return indexerResult.GetHomeData;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "getRarityRank error");
            return new HomeDataDto();
        }
    }


    public async Task<List<NFTActivityIndexDto>> GetSchrodingerSoldListAsync(GetSchrodingerSoldInput input)
    {
        try
        {
            var res = await _graphQlHelper.QueryAsync<SchrodingerSoldListQueryDto>(new GraphQLRequest
            {
                Query = @"query (
                    $timestampMax:Long!,
                    $timestampMin:Long!,
                    $chainId:String!
                ){
                  getSchrodingerSoldList(
                    input:{
                      timestampMax:$timestampMax,
                      timestampMin:$timestampMin,
                      chainId:$chainId
                    }
                  ){
                        id,
                        from,
                        to,
                        type,
                        nftInfoId,
                        amount,
                        price,
                        timestamp,
                        transactionHash
                  }
                }",
                Variables = new
                {
                    timestampMin = input.TimestampMin,
                    timestampMax = input.TimestampMax,
                    chainId = input.ChainId
                }
            });
            return res.GetSchrodingerSoldList;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "getSchrodingerSoldList query GraphQL error");
            throw;
        }
    }
    
    
    public async Task<HoldingPointBySymbolDto> GetHoldingPointBySymbolAsync(string symbol, string chainId)
    {
        try
        {
            var res = await _graphQlHelper.QueryAsync<HoldingPointBySymbolQueryDto>(new GraphQLRequest
            {
                Query = @"query (
                    $symbol:String!,
                    $chainId:String!
                ){
                  getHoldingPointBySymbol(
                    input:{
                      symbol:$symbol,
                      chainId:$chainId
                    }
                  ){
                        point,
                        level
                  }
                }",
                Variables = new
                {
                    symbol = symbol,
                    chainId = chainId
                }
            });
            return res.GetHoldingPointBySymbol;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetHoldingPointBySymbol query GraphQL error");
            return  new HoldingPointBySymbolDto();
        }
    }
    
    public async Task<SchrodingerIndexerListDto> GetSchrodingerHoldingListAsync(GetCatListInput input)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<SchrodingerHoldingIndexerQuery>(new GraphQLRequest
            {
                Query =
                    @"query($chainId:String!, $address:String!, $skipCount:Int!,$maxResultCount:Int!){
                    getSchrodingerHoldingList(input: {chainId:$chainId,address:$address,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalCount,
                        data{
                        symbol,
                        tokenName,
                        amount,
                        generation,
                        decimals,
                        adopter,
                        address
                    }
                }
            }",
                Variables = new
                {
                    chainId = input.ChainId ?? "", address = input.Address ?? "",
                    skipCount = input.SkipCount, maxResultCount = input.MaxResultCount
                }
            });

            return indexerResult.GetSchrodingerHoldingList;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetSchrodingerHoldingList Indexer error");
            return new SchrodingerIndexerListDto();
        }
    }
}