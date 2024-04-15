using GraphQL;
using Microsoft.Extensions.Logging;
using SchrodingerServer.Worker.Core.Common;
using SchrodingerServer.Worker.Core.Dtos;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Worker.Core.Provider;

public interface IIndexerProvider
{
    public Task<List<string>> SubscribeConfirmedAsync(string chainId, long to, long from);
    public Task<long> GetIndexBlockHeightAsync(string chainId);
}

public class IndexerProvider : IIndexerProvider, ISingletonDependency
{
    private readonly IGraphQLHelper _graphQlHelper;
    private readonly ILogger<IndexerProvider> _logger;

    public IndexerProvider(IGraphQLHelper graphQlHelper, ILogger<IndexerProvider> logger)
    {
        _graphQlHelper = graphQlHelper;
        _logger = logger;
    }

    public async Task<List<string>> SubscribeConfirmedAsync(string chainId, long to, long from)
    {
        try
        {
            var indexerResult = await _graphQlHelper.QueryAsync<IndexerConfirmedListDto>(new GraphQLRequest
            {
                Query =
                    @"query($chainId:String!,$fromBlockHeight:Long!,$toBlockHeight:Long!){
                    getAdoptInfoList(input: {chainId:$chainId,fromBlockHeight:$fromBlockHeight,toBlockHeight:$toBlockHeight}){
                        transactionIds
                }
            }",
                Variables = new
                {
                    chainId = chainId, fromBlockHeight = from, toBlockHeight = to
                }
            });
            return indexerResult.GetAdoptInfoList == null
                ? new List<string>()
                : indexerResult.GetAdoptInfoList.TransactionIds;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Indexer] SubscribeLogs failed.");
            return new List<string>();
        }


        // return new IndexerConfirmedListDto
        // {
        //     TransactionIds = new List<ConfirmedDto>
        //     {
        //         new()
        //         {
        //             ChainId = "tDVW",
        //             TransactionId = "f161a0bf79dd09a130013bb3f4aaa5c67a53ed20c4539c50e8387012b73ff398"
        //         }
        //     }
        // };
    }

    public async Task<long> GetIndexBlockHeightAsync(string chainId)
    {
        try
        {
            var res = await _graphQlHelper.QueryAsync<ConfirmedBlockHeightRecord>(new GraphQLRequest
            {
                Query = @"
			    query($chainId:String!,$filterType:BlockFilterType!) {
                    syncState(input: {chainId:$chainId,filterType:$filterType}){
                        confirmedBlockHeight
                }
            }",
                Variables = new
                {
                    chainId, filterType = BlockFilterType.LOG_EVENT
                }
            });

            return res.SyncState.ConfirmedBlockHeight;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Indexer] GetIndexBlockHeight failed.");
            return 0;
        }
    }
}