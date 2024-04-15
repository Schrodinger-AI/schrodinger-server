using GraphQL;
using GraphQL.Client.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchrodingerServer.Worker.Core.Options;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Worker.Core.Common;

public interface IGraphQLHelper
{
    Task<T> QueryAsync<T>(GraphQLRequest request);
}

public class GraphQLHelper : IGraphQLHelper, ISingletonDependency
{
    private readonly IGraphQLClient _client;
    private readonly GraphqlOptions _options;
    private readonly ILogger<GraphQLHelper> _logger;

    public GraphQLHelper(IGraphQLClient client, ILogger<GraphQLHelper> logger, IOptionsSnapshot<GraphqlOptions> options)
    {
        _client = client;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<T> QueryAsync<T>(GraphQLRequest request)
    {
        return await SendQueryAsync<T>(request);
    }

    private async Task<T> SendQueryAsync<T>(GraphQLRequest request)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.QueryTimeout));
        var startTime = DateTime.Now;

        try
        {
            var graphQlResponse = await _client.SendQueryAsync<T>(request, cts.Token);
            if (graphQlResponse.Errors is not { Length: > 0 })
            {
                return graphQlResponse.Data;
            }

            _logger.LogError("[GraphQLHelper] Query graphQL err, errors = {Errors}",
                string.Join(",", graphQlResponse.Errors.Select(e => e.Message).ToList()));
            return default;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("[GraphQLHelper] Query graphQL timed out, Took {time} ms.",
                DateTime.Now.Subtract(startTime).TotalMilliseconds);
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[GraphQLHelper] Query graphQL fail.");
            throw;
        }
    }
}