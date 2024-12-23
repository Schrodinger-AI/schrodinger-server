using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AElf.Indexing.Elasticsearch;
using GraphQL;
using Microsoft.Extensions.Logging;
using Nest;
using SchrodingerServer.Common.GraphQL;
using SchrodingerServer.ExceptionHandling;
using SchrodingerServer.Users.Index;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Users;

public interface IHolderBalanceProvider
{
    Task<List<HolderDailyChangeDto>> GetHolderDailyChangeListAsync(string chainId, string bizDate, int skipCount,
        int maxResultCount, string symbol);

    Task<Dictionary<string, HolderBalanceIndex>> GetHolderBalanceAsync(string chainId, List<string> ids);
    
    Task<List<HolderBalanceIndex>> GetPreHolderBalanceListAsync(string chainId, string bizDate, int skipCount,
        int maxResultCount);
    
    Task<HolderDailyChangeDto> GetLastHoldingRecordAsync(string chainId, string address, string symbol, List<string> excludeDate);
}

public class HolderBalanceProvider : IHolderBalanceProvider, ISingletonDependency
{
    private readonly IGraphQlHelper _graphQlHelper;
    private readonly INESTRepository<HolderBalanceIndex, string> _holderBalanceIndexRepository;
    private readonly ILogger<HolderBalanceProvider> _logger;

    public HolderBalanceProvider(IGraphQlHelper graphQlHelper,
        INESTRepository<HolderBalanceIndex, string> holderBalanceIndexRepository,
        ILogger<HolderBalanceProvider> logger)
    {
        _graphQlHelper = graphQlHelper;
        _holderBalanceIndexRepository = holderBalanceIndexRepository;
        _logger = logger;
    }


    public async Task<List<HolderDailyChangeDto>> GetHolderDailyChangeListAsync(string chainId, string date,
        int skipCount, int maxResultCount, string symbol)
    {
        var graphQlResponse = await _graphQlHelper.QueryAsync<IndexerHolderDailyChangeDto>(new GraphQLRequest
        {
            Query = @"query($chainId:String!,$date:String!,$skipCount:Int!,$maxResultCount:Int!,$symbol:String!){
            getSchrodingerHolderDailyChangeList(input: {chainId:$chainId,date:$date,skipCount:$skipCount,maxResultCount:$maxResultCount,symbol:$symbol})
            {
               data {
                address,
                symbol,
                date,
                changeAmount,
                balance
                },
                totalCount
            }}",
            Variables = new
            {
                chainId,
                date,
                skipCount,
                maxResultCount,
                symbol
            }
        });
        return graphQlResponse?.GetSchrodingerHolderDailyChangeList.Data;
    }

    public async Task<Dictionary<string, HolderBalanceIndex>> GetHolderBalanceAsync(string chainId, List<string> ids)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<HolderBalanceIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.ChainId).Value(chainId)));

        mustQuery.Add(q => q.Terms(i =>
            i.Field(f => f.Id).Terms(ids)));

        QueryContainer Filter(QueryContainerDescriptor<HolderBalanceIndex> f) =>
            f.Bool(b => b.Must(mustQuery));
        
        var tuple = await _holderBalanceIndexRepository.GetSortListAsync(Filter);
        
        return !tuple.Item2.IsNullOrEmpty()
            ? tuple.Item2.ToDictionary(item => item.Id, item => item)
            : new Dictionary<string, HolderBalanceIndex>();
    }

    public async Task<List<HolderBalanceIndex>> GetPreHolderBalanceListAsync(string chainId, string bizDate, int skipCount, int maxResultCount)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<HolderBalanceIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.ChainId).Value(chainId)));

        mustQuery.Add(q => q.TermRange(i
            => i.Field(index => index.BizDate).LessThan(bizDate)));
        
        mustQuery.Add(q => q.Range(i
            => i.Field(index => index.Balance).GreaterThan(0)));

        QueryContainer Filter(QueryContainerDescriptor<HolderBalanceIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var tuple = await _holderBalanceIndexRepository.GetListAsync(Filter, skip: skipCount, limit: maxResultCount);
        
        return !tuple.Item2.IsNullOrEmpty() ? tuple.Item2 : new List<HolderBalanceIndex>();
    }
    
    public async Task<HolderDailyChangeDto> GetLastHoldingRecordAsync(string chainId, string address, string symbol, List<string>  excludeDate)
    {
        try
        {
            var graphQlResponse = await _graphQlHelper.QueryAsync<IndexerHolderDailyChangeDto>(new GraphQLRequest
            {
                Query =
                    @"query($chainId:String!,$skipCount:Int!,$maxResultCount:Int!,$address:String!,$symbol:String!,$excludeDate:[String!]){
            getSchrodingerHolderDailyChangeList(input: {chainId:$chainId,skipCount:$skipCount,maxResultCount:$maxResultCount, address:$address, symbol:$symbol, excludeDate:$excludeDate})
            {
               data {
                address,
                symbol,
                date,
                changeAmount,
                balance
                },
                totalCount
            }}",
                Variables = new
                {
                    chainId = chainId,
                    skipCount = 0,
                    maxResultCount = 1000,
                    address = address,
                    symbol = symbol,
                    excludeDate = excludeDate
                }
            });
            return graphQlResponse?.GetSchrodingerHolderDailyChangeList.Data?.LastOrDefault();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetLastHoldingRecordAsync Error, address: {address}, symbol: {symbol}, excludeDate: {excludeDate}", address, symbol, excludeDate);
            return null;
        }
    }
}