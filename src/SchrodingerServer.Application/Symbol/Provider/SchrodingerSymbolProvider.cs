using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using SchrodingerServer.Common;
using SchrodingerServer.Common.GraphQL;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Symbol.Provider;

public interface ISchrodingerSymbolProvider
{
    Task<List<SchrodingerSymbolDto>> GetSchrodingerSymbolList( int skipCount, int maxResultCount);
}

public class SchrodingerSymbolProvider : ISchrodingerSymbolProvider, ISingletonDependency
{
    private readonly IGraphQlHelper _graphQlHelper;

    public SchrodingerSymbolProvider(IGraphQlHelper graphQlHelper)
    {
        _graphQlHelper = graphQlHelper;
    }


    public async Task<List<SchrodingerSymbolDto>> GetSchrodingerSymbolList(
        int skipCount, int maxResultCount)
    {
        var graphQlResponse = await _graphQlHelper.QueryAsync<GetSchrodingerSymbolList>(new GraphQLRequest
        {
            Query = @"query($skipCount:Int!,$maxResultCount:Int!){
            data:getSchrodingerSymbolList(input: {skipCount:$skipCount,maxResultCount:$maxResultCount})
            {
                totalCount,     
                data{
                    symbol
                }
            }}",
            Variables = new
            {
                skipCount,
                maxResultCount
            }
        });
        return graphQlResponse?.Data.Data ?? new List<SchrodingerSymbolDto>();
    }
}

public class GetSchrodingerSymbolList : IndexerCommonResult<SchrodingerSymbolListDto>
{
    public SchrodingerSymbolListDto Data { get; set; }
    
}
public class SchrodingerSymbolListDto
{
    public SchrodingerSymbolListDto(long totalCount, List<SchrodingerSymbolDto> data)
    {
        TotalCount = totalCount;
        Data = data;
    }

    public long TotalCount { get; set; }
    
    public List<SchrodingerSymbolDto> Data { get; set; }
}
    
public class SchrodingerSymbolDto 
{
    public string Symbol { get; set; }
}