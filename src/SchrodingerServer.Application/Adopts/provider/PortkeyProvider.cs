using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using GraphQL;
using Microsoft.Extensions.Logging;
using SchrodingerServer.Awaken.Provider;
using SchrodingerServer.Common.GraphQL;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Adopts.provider;

public interface IPortkeyProvider
{
    Task<IsEOAAddressDto> IsEOAAddress(string address);
    Task<List<CaHolderInfo>> BatchGetAddressInfo(List<string> addressList);
}

public class PortkeyProvider : IPortkeyProvider, ISingletonDependency
{
    private readonly IGraphQLClientFactory _graphQlClientFactory;
    private readonly ILogger<PortkeyProvider> _logger;
    
    public PortkeyProvider(
        IGraphQLClientFactory graphQlClientFactory, 
        ILogger<PortkeyProvider> logger)
    {
        _graphQlClientFactory = graphQlClientFactory;
        _logger = logger;
    }
    
    [ExceptionHandler(typeof(Exception), ReturnDefault = ReturnDefault.Default)]
    public async Task<IsEOAAddressDto> IsEOAAddress(string address)
    {
        var res = await _graphQlClientFactory.GetClient(GraphQLClientEnum.PortkeyClient).SendQueryAsync<CaHolderInfoDto>(new GraphQLRequest
        {
            Query = @"query (
                    $skipCount:Int!,
                    $maxResultCount:Int!,
                    $address:[String!]
                ){ 
                    caHolderInfo(dto: { 
                                caAddresses: $address, 
                                maxResultCount: $maxResultCount, 
                                skipCount: $skipCount }) 
                    {     
                        id 
                        chainId 
                        caHash 
                        caAddress 
                        originChainId 
                         
                    }
                }",
            Variables = new
            {
                address = address, 
                skipCount = 0, 
                maxResultCount = 10000,
            }
        });
        return new IsEOAAddressDto()
        {
            IsEOAAddress = res.Data.CaHolderInfo.Count == 0,
        };
    }
    
    [ExceptionHandler(typeof(Exception), ReturnDefault = ReturnDefault.Default)]
    public async Task<List<CaHolderInfo>> BatchGetAddressInfo(List<string> addressList)
    {
        var res = await _graphQlClientFactory.GetClient(GraphQLClientEnum.PortkeyClient).SendQueryAsync<CaHolderInfoDto>(new GraphQLRequest
        {
            Query = @"query (
                    $skipCount:Int!,
                    $maxResultCount:Int!,
                    $addressList:[String!]
                ){ 
                    caHolderInfo(dto: { 
                                caAddresses: $addressList, 
                                maxResultCount: $maxResultCount, 
                                skipCount: $skipCount }) 
                    {     
                        id 
                        chainId 
                        caHash 
                        caAddress 
                        originChainId 
                         
                    }
                }",
            Variables = new
            {
                addressList = addressList, 
                skipCount = 0, 
                maxResultCount = 10000,
            }
        });
        return res.Data.CaHolderInfo;
    }
    
}

public class IsEOAAddressDto
{
    public bool IsEOAAddress { get; set; }
}

public class CaHolderInfoDto
{
    public List<CaHolderInfo> CaHolderInfo { get; set; }
}

public class CaHolderInfo
{
    public string Id { get; set; }
    public string ChainId { get; set; }
    public string CaHash { get; set; }
    public string CaAddress { get; set; }
}