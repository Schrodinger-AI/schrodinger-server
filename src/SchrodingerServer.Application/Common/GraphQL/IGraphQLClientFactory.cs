using GraphQL.Client.Abstractions;

namespace SchrodingerServer.Common.GraphQL;

public interface IGraphQLClientFactory
{
    IGraphQLClient GetClient(GraphQLClientEnum clientEnum);
}

public enum GraphQLClientEnum
{
    ForestClient,
    SchrodingerClient,
    PointPlatform
}