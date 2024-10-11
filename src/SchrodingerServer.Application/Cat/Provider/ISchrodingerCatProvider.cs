using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using GraphQL;
using Microsoft.Extensions.Logging;
using SchrodingerServer.Cat.Provider.Dtos;
using SchrodingerServer.Common.GraphQL;
using SchrodingerServer.Dtos.Cat;
using SchrodingerServer.ExceptionHandling;
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
    
    Task<List<NFTActivityIndexDto>> GetSchrodingerTradeRecordAsync(GetSchrodingerTradeRecordInput input);

    Task<SchrodingerIndexerBoxListDto> GetSchrodingerBoxListAsync(GetBlindBoxListInput input);
    
    Task<SchrodingerIndexerBoxDto> GetSchrodingerBoxDetailAsync(GetCatDetailInput input);
    
    Task<SchrodingerIndexerStrayCatsDto> GetStrayCatsListAsync(StrayCatsInput input);
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

    [ExceptionHandler(typeof(Exception), Message = "GetSchrodingerCatList Indexer error", ReturnDefault = ReturnDefault.New)]
    public async Task<SchrodingerIndexerListDto> GetSchrodingerCatListAsync(GetCatListInput input)
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

    [ExceptionHandler(typeof(Exception), Message = "GetSchrodingerAllCatsListAsync Indexer error", ReturnDefault = ReturnDefault.New)]
    public async Task<SchrodingerSymbolIndexerListDto> GetSchrodingerAllCatsListAsync(GetCatListInput input)
    {
        var indexerResult = await _graphQlHelper.QueryAsync<SchrodingerSymbolIndexerQuery>(new GraphQLRequest
        {
            Query =
                @"query($keyword:String!, $chainId:String!, $tick:String!, $traits:[TraitsInput!],$raritys:[String!],$generations:[Int!],$skipCount:Int!,$maxResultCount:Int!,$filterSgr:Boolean!,$minAmount:String!){
                    getAllSchrodingerList(input: {keyword:$keyword,chainId:$chainId,tick:$tick,traits:$traits,raritys:$raritys,generations:$generations,skipCount:$skipCount,maxResultCount:$maxResultCount,filterSgr:$filterSgr,minAmount:$minAmount}){
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
                skipCount = input.SkipCount, maxResultCount = input.MaxResultCount,filterSgr = input.FilterSgr,
                minAmount = input.MinAmount
            }
        });

        return indexerResult.GetAllSchrodingerList;
    }
    
    [ExceptionHandler(typeof(Exception), Message = "GetSchrodingerCatDetailAsync Indexer error", ReturnDefault = ReturnDefault.New)]
    public async Task<SchrodingerDetailDto> GetSchrodingerCatDetailAsync(GetCatDetailInput input)
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
    
    [ExceptionHandler(typeof(Exception), Message = "GetSchrodingerCatRankAsync Indexer error", ReturnDefault = ReturnDefault.New)]
    public async Task<CatRankDto> GetSchrodingerCatRankAsync(GetCatRankInput input)
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
    
    [ExceptionHandler(typeof(Exception), Message = "GetHoldingRankAsync Indexer error", ReturnDefault = ReturnDefault.New)]
    public async Task<List<RankItem>> GetHoldingRankAsync()
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
    
    [ExceptionHandler(typeof(Exception), Message = "GetRarityRankAsync Indexer error", ReturnDefault = ReturnDefault.New)]
    public async Task<List<RarityRankItem>> GetRarityRankAsync()
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
    
    [ExceptionHandler(typeof(Exception), Message = "GetHomeDataAsync Indexer error", ReturnDefault = ReturnDefault.New)]
    public async Task<HomeDataDto> GetHomeDataAsync(string chainId)
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
    
    public async Task<List<NFTActivityIndexDto>> GetSchrodingerSoldListAsync(GetSchrodingerSoldInput input)
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
    
    [ExceptionHandler(typeof(Exception), Message = "GetHoldingPointBySymbolAsync Indexer error", ReturnDefault = ReturnDefault.New)]
    public async Task<HoldingPointBySymbolDto> GetHoldingPointBySymbolAsync(string symbol, string chainId)
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
    
    [ExceptionHandler(typeof(Exception), Message = "GetSchrodingerHoldingListAsync Indexer error", ReturnDefault = ReturnDefault.New)]
    public async Task<SchrodingerIndexerListDto> GetSchrodingerHoldingListAsync(GetCatListInput input)
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
    
     public async Task<List<NFTActivityIndexDto>> GetSchrodingerTradeRecordAsync(GetSchrodingerTradeRecordInput input)
    {
        var res = await _graphQlHelper.QueryAsync<SchrodingerTradeRecordQueryDto>(new GraphQLRequest
        {
            Query = @"query (
                    $symbol:String!,
                    $buyer:String!,
                    $chainId:String!,
                    $tradeTime:DateTime!
                ){
                  getSchrodingerTradeRecord(
                    input:{
                      symbol:$symbol,
                      buyer:$buyer,
                      chainId:$chainId,
                      tradeTime:$tradeTime
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
                symbol = input.Symbol,
                buyer = input.Buyer,
                chainId = input.ChainId,
                tradeTime = input.TradeTime
            }
        });
        return res.GetSchrodingerTradeRecord;
    }
    
    [ExceptionHandler(typeof(Exception), Message = "GetSchrodingerBoxListAsync Indexer error", ReturnDefault = ReturnDefault.New)]
    public async Task<SchrodingerIndexerBoxListDto> GetSchrodingerBoxListAsync(GetBlindBoxListInput input)
    {
        var indexerResult = await _graphQlHelper.QueryAsync<SchrodingerIndexerBoxListQuery>(new GraphQLRequest
        {
            Query =
                @"query($adopter:String!, $adoptTime:Long!){
                    getBlindBoxList(input: {adopter:$adopter, adoptTime:$adoptTime}){
                        totalCount,
                        data{
                        symbol,
                        tokenName,
                        adoptId,
                        amount,
                        gen,
                        decimals,
                        adopter,
                        adoptTime,
                        rarity,
                        rank,
                        traits{traitType,value,isRare,percent}
                    }
                }
            }",
            Variables = new
            {
                Adopter = input.Address,
                AdoptTime = input.AdoptTime
            }
        });

        return indexerResult.GetBlindBoxList;
    }
    
    [ExceptionHandler(typeof(Exception), Message = "GetSchrodingerBoxDetailAsync Indexer error", ReturnDefault = ReturnDefault.New)]
    public async Task<SchrodingerIndexerBoxDto> GetSchrodingerBoxDetailAsync(GetCatDetailInput input)
    {
        var indexerResult = await _graphQlHelper.QueryAsync<SchrodingerIndexerBoxDetailQuery>(new GraphQLRequest
        {
            Query =
                @"query($symbol:String!){
                    getBlindBoxDetail(input: {symbol:$symbol}){
                        symbol,
                        tokenName,
                        adoptId,
                        amount,
                        gen,
                        decimals,
                        adopter,
                        adoptTime,
                        rarity,
                        rank,
                        traits{traitType,value,percent,isRare}
                        consumeAmount,
                        directAdoption
                }
            }",
            Variables = new
            {
                Symbol = input.Symbol
            }
        });

        return indexerResult.GetBlindBoxDetail;
    }
    
    [ExceptionHandler(typeof(Exception), Message = "GetStrayCatsListAsync Indexer error", ReturnDefault = ReturnDefault.New)]
    public async Task<SchrodingerIndexerStrayCatsDto> GetStrayCatsListAsync(StrayCatsInput input)
    {
        var indexerResult = await _graphQlHelper.QueryAsync<SchrodingerIndexerStrayCatsQuery>(new GraphQLRequest
        {
            Query =
                @"query($adopter:String!, $chainId:String!, $skipCount:Int!, $maxResultCount:Int!, $adoptTime:Long!){
                    getStrayCats(input: {adopter:$adopter, chainId:$chainId, skipCount:$skipCount, maxResultCount:$maxResultCount adoptTime:$adoptTime}){
                        totalCount,
                        data{
                        adoptId, 
                        inscriptionImageUri,
                        tokenName, 
                        gen,
                        symbol,
                        consumeAmount,
                        receivedAmount,
                        decimals,
                        parentTraits{traitType,value},
                        nextTokenName,
                        nextSymbol,
                        nextAmount,
                        directAdoption
                    }
                }
            }",
            Variables = new
            {
                Adopter = input.Adopter,
                SkipCount = input.SkipCount,
                MaxResultCount = input.MaxResultCount,
                AdoptTime = input.AdoptTime,
                ChainId = input.ChainId
            }
        });

        return indexerResult.GetStrayCats;
    }
}