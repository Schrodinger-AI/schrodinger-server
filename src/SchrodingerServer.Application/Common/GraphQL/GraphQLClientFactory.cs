using System.Collections.Concurrent;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Common.GraphQL
{
    public class GraphQLClientFactory : IGraphQLClientFactory, ISingletonDependency
    {
        private readonly GraphQLOptions _graphQlClientOptions;
        private readonly ConcurrentDictionary<string, IGraphQLClient> _clientDic;
        private static readonly object lockObject = new object();

        public GraphQLClientFactory(IOptionsSnapshot<GraphQLOptions> graphQlClientOptions)
        {
            _graphQlClientOptions = graphQlClientOptions.Value;
            _clientDic = new ConcurrentDictionary<string, IGraphQLClient>();
        }

        public IGraphQLClient GetClient(GraphQLClientEnum clientEnum)
        {
            var clientName = clientEnum.ToString();

            if (_clientDic.TryGetValue(clientName, out var client))
            {
                return client;
            }

            lock (lockObject)
            {
                if (!_clientDic.TryGetValue(clientName, out client))
                {
                    // client = clientEnum == GraphQLClientEnum.ForestClient
                    //     ? new GraphQLHttpClient(_graphQlClientOptions.ForestConfiguration,
                    //         new NewtonsoftJsonSerializer())
                    //     : new GraphQLHttpClient(_graphQlClientOptions.Configuration,
                    //         new NewtonsoftJsonSerializer());

                    switch (clientEnum)
                    {
                        case GraphQLClientEnum.ForestClient:
                            client = new GraphQLHttpClient(_graphQlClientOptions.ForestConfiguration,
                                new NewtonsoftJsonSerializer());
                            break;
                        case GraphQLClientEnum.SchrodingerClient:
                            client = new GraphQLHttpClient(_graphQlClientOptions.Configuration,
                                new NewtonsoftJsonSerializer());
                            break;
                        case GraphQLClientEnum.PointPlatform:
                            client = new GraphQLHttpClient(_graphQlClientOptions.PointPlatformConfiguration,
                                new NewtonsoftJsonSerializer());
                            break;
                    }

                    _clientDic[clientName] = client;
                }
            }

            return client;
        }
    }
}