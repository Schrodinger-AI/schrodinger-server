using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using GraphQL;
using Microsoft.Extensions.Logging;
using Nest;
using SchrodingerServer.Common.GraphQL;
using SchrodingerServer.Message.Dtos;
using SchrodingerServer.Message.Provider.Dto;
using SchrodingerServer.Users.Index;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Message.Provider;

public interface IMessageProvider
{
    Task<List<string>> GetAllReadMessagesAsync(string address);

    Task<NFTActivityIndexListDto> GetSchrodingerSoldListAsync(GetSchrodingerSoldListInput dto);

    Task<List<string>> GetAllSchrodingerSoldIdAsync(GetSchrodingerSoldListInput input);
    
    Task MarkMessageReadAsync(List<ReadMessageIndex> readMessageList);
}

public class MessageProvider : IMessageProvider, ISingletonDependency
{
    private readonly INESTRepository<ReadMessageIndex, string> _readMessageIndexRepository;
    private readonly IGraphQLClientFactory _graphQlClientFactory;
    private readonly ILogger<MessageProvider> _logger;
    private readonly long StartTimestamp = 1715414400000;
    
    
    public MessageProvider(
        INESTRepository<ReadMessageIndex, string> readMessageIndexRepository, 
        IGraphQLClientFactory graphQlClientFactory, 
        ILogger<MessageProvider> logger)
    {
        _readMessageIndexRepository = readMessageIndexRepository;
        _graphQlClientFactory = graphQlClientFactory;
        _logger = logger;
    }

    public async Task<List<string>> GetAllReadMessagesAsync(string address)
    {
        var res = new List<ReadMessageIndex>();
        List<ReadMessageIndex> list;
        var skipCount = 0;
        var mustQuery = new List<Func<QueryContainerDescriptor<ReadMessageIndex>, QueryContainer>>();
        
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.Address).Value(address)));
        
        QueryContainer Filter(QueryContainerDescriptor<ReadMessageIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        
        do
        {
            var listDto = await _readMessageIndexRepository.GetListAsync(Filter, skip: skipCount, limit: 1000,
                sortType: SortOrder.Ascending, sortExp: o => o.CreateTime);
            list = listDto.Item2;
            var count = list.Count;
            res.AddRange(list);
            if (list.IsNullOrEmpty() || count < 1000)
            {
                break;
            }
            
            skipCount += count;
        } while (!list.IsNullOrEmpty());

        return res.Select(x => x.MessageId).ToList();
    }


    public async Task<NFTActivityIndexListDto> GetSchrodingerSoldListAsync(GetSchrodingerSoldListInput input)
    {
        try
        {
            var res = await _graphQlClientFactory.GetClient(GraphQLClientEnum.SchrodingerClient).SendQueryAsync<NFTActivityIndexListQueryDto>(new GraphQLRequest
            {
                Query = @"query (
                    $skipCount:Int!,
                    $maxResultCount:Int!,
                    $filterSymbol:String!,
                    $address:String!,
                    $timestampMin:Long!,
                    $chainId:String!,
                    $types:[Int!],
                    $sortType:String!
                ){
                  getSchrodingerSoldRecord(
                    input:{
                      skipCount:$skipCount,
                      maxResultCount:$maxResultCount,
                      filterSymbol:$filterSymbol,
                      address:$address,
                      timestampMin:$timestampMin,
                      chainId:$chainId,
                      types:$types,
                      sortType:$sortType
                    }
                  ){
                    totalRecordCount,
                    data{
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
                  }
                }",
                Variables = new
                {
                    skipCount = input.SkipCount, 
                    maxResultCount = input.MaxResultCount,
                    filterSymbol = input.FilterSymbol,
                    address = input.Address,
                    timestampMin = StartTimestamp,
                    chainId = input.ChainId,
                    types = new List<int>(),
                    sortType = ""
                }
            });
            return res.Data?.GetSchrodingerSoldRecord;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "getSchrodingerSoldRecord query GraphQL error");
            throw;
        }
    }
    
    public async Task<List<string>> GetAllSchrodingerSoldIdAsync(GetSchrodingerSoldListInput input)
    {
        var res = new List<string>();
        List<string> list;
        var skipCount = 0;

        List<NFTActivityIndexDto> soldList;

        do
        {
            var soldListDto = await GetSchrodingerSoldListAsync(input);
            soldList = soldListDto.Data;
            var count = soldList.Count;
            res.AddRange(soldList.Select(x => x.Id).ToList());
            if (soldList.IsNullOrEmpty() || count < 1000)
            {
                break;
            }
            skipCount += count;
            input.SkipCount = skipCount;
        } while (!soldList.IsNullOrEmpty());

        return res;
    }

    public async Task MarkMessageReadAsync(List<ReadMessageIndex> readMessageList)
    {
        await _readMessageIndexRepository.BulkAddOrUpdateAsync(readMessageList);
    }
}




