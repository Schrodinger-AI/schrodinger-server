using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Client.Dto;
using AElf.Client.Service;
using AElf.Types;
using Google.Protobuf;
using GraphQL;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Points.Contracts.Point;
using SchrodingerServer.Background.Dtos;
using SchrodingerServer.Common.GraphQL;
using SchrodingerServer.Options;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Background.Providers;

public interface IBalanceProvider
{
    Task<GetPointsBalanceOutput> GetPointsBalanceOutputAsync(string address);
    Task<List<RankingDetailIndexerDto>> GetOperatorPointsActionSumAsync(string address);
}

public class BalanceProvider : IBalanceProvider, ISingletonDependency
{
    private readonly ILogger<BalanceProvider> _logger;
    private readonly PointContractOptions _options;
    private readonly IGraphQLClientFactory _graphQlClientFactory;

    public BalanceProvider(ILogger<BalanceProvider> logger, IOptionsSnapshot<PointContractOptions> options,
        IGraphQLClientFactory graphQlClientFactory)
    {
        _logger = logger;
        _graphQlClientFactory = graphQlClientFactory;
        _options = options.Value;
    }

    public async Task<GetPointsBalanceOutput> GetPointsBalanceOutputAsync(string address)
    {
        var pointDtos = await GetOperatorPointsActionSumAsync(address);
        var pointDto = pointDtos.FirstOrDefault();
        
        var param = new GetPointsBalanceInput();
        param.Address = Address.FromBase58(address);
        param.PointName = "XPSGR-4";
        param.DappId = Hash.LoadFromHex(pointDto.DappId);
        param.Domain = pointDto.Domain;
        
        var output = await CallTransactionAsync<GetPointsBalanceOutput>("GetPointsBalance", param,
            _options.ContractAddress, _options.ChainId);
        return output;
    }

    private async Task<T> CallTransactionAsync<T>(string methodName, IMessage param, string contractAddress,
        string chainId) where T : class, IMessage<T>, new()
    {
        var client = new AElfClient(_options.BaseUrl);
        await client.IsConnectedAsync();

        var addressFromPrivateKey = client.GetAddressFromPrivateKey(_options.CommonPrivateKeyForCallTx);

        var transaction =
            await client.GenerateTransactionAsync(addressFromPrivateKey, contractAddress, methodName, param);

        _logger.LogDebug("Call tx methodName is: {methodName} param is: {transaction}", methodName, transaction);

        var txWithSign = client.SignTransaction(_options.CommonPrivateKeyForCallTx, transaction);

        var result = await client.ExecuteTransactionAsync(new ExecuteTransactionDto
        {
            RawTransaction = txWithSign.ToByteArray().ToHex()
        });

        var value = new T();
        value.MergeFrom(ByteArrayHelper.HexStringToByteArray(result));

        return value;
    }

    public async Task<List<RankingDetailIndexerDto>> GetOperatorPointsActionSumAsync(
        string address)
    {
        var indexerResult = await _graphQlClientFactory.GetClient(GraphQLClientEnum.PointPlatform).SendQueryAsync<RankingDetailIndexerQueryDto>(new GraphQLRequest
        {
            Query =
                @"query($dappId:String!, $address:String!, $domain:String!){
                    getPointsSumByAction(input: {dappId:$dappId,address:$address,domain:$domain}){
                        totalRecordCount,
                        data{
                        id,
                        address,
                        domain,
                        role,
                        dappId,
    					pointsName,
    					actionName,
    					amount,
    					createTime,
    					updateTime
                    }
                }
            }",
            Variables = new
            {
                dappId = string.Empty,
                domain = string.Empty,
                address = address
            }
        });

        return indexerResult.Data.GetPointsSumByAction.Data;
    }
}