using System;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Common.GraphQL;

public interface IGraphQlHelper
{
    Task<T> QueryAsync<T>(GraphQLRequest request);
}

public class GraphQlHelper : IGraphQlHelper, ISingletonDependency
{
    private readonly IGraphQLClient _client;
    private readonly ILogger<GraphQlHelper> _logger;

    public GraphQlHelper(IGraphQLClient client, ILogger<GraphQlHelper> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<T> QueryAsync<T>(GraphQLRequest request)
    {
        var graphQlResponse = await _client.SendQueryAsync<T>(request);
        if (graphQlResponse.Errors is not { Length: > 0 })
        {
            return graphQlResponse.Data;
        }

        _logger.LogError("query graphQL err, errors = {Errors}", string.Join(",", graphQlResponse.Errors.Select(e => e.Message).ToList()));
        return default;
    }
}

public class GraphQlResponseException : Exception
{
    public GraphQlResponseException(string message) : base(message)
    {
    }
}