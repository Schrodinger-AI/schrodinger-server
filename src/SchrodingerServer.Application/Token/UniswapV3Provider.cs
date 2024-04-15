using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SchrodingerServer.Common;
using SchrodingerServer.Options;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Token;

public class UniswapV3Provider : ISingletonDependency
{

    private readonly ILogger<UniswapV3Provider> _logger;
    private readonly GraphQLHttpClient _client;
    private readonly IOptionsMonitor<UniswapV3Options> _uniswapOptions;
    
    public UniswapV3Provider(IOptionsMonitor<UniswapV3Options> uniswapOptions,
        ILogger<UniswapV3Provider> logger)
    {
        _uniswapOptions = uniswapOptions;
        _client = new GraphQLHttpClient(_uniswapOptions.CurrentValue.BaseUrl, new NewtonsoftJsonSerializer());
        _logger = logger;
    }
    

    public async Task<TokenResponse> GetLatestUSDPriceAsync(long date)
    {
        var tokenId = _uniswapOptions.CurrentValue.TokenId;
        if (tokenId.IsNullOrEmpty())
        {
            return new TokenResponse
            {
                Id = date.ToString(),
                Date = date,
                PriceUSD = _uniswapOptions.CurrentValue.DefaultBasePrice,
            };
        }
        var resp = await _client.SendQueryAsync<ResponseWrapper<List<TokenResponse>>>(new GraphQLRequest
        {
            Query = @"query($tokenId:String, $date:Int!){
                        data:tokenDayDatas(
                                where: {token_: {id: $tokenId}, date: $date}
                         ) {
                             date
                             id
                             priceUSD
                             token {
                                    id
                                    name
                                    symbol
                             }
                         }
                    }",
            Variables = new
            {
                tokenId = tokenId, 
                date = date
            }
        });
        _logger.LogDebug("UniSwapV3 price  tokenId={tokenId}, resp={Resp}", tokenId,
            JsonConvert.SerializeObject(resp));
        if (resp.Data != null && !resp.Data!.Data.IsNullOrEmpty())
        {
           return resp.Data.Data[0];
        }
        return null;
    }

    public class BaseResponse<T>
    {
        public ResponseWrapper<T> Data { get; set; }

        public T GetData()
        {
            return Data.Data;
        }
    }

    public class ResponseWrapper<T>
    {
        public T Data { get; set; }
    }
    
    public class TokenResponse
    {
        public string Id { get; set; }
        public long Date { get; set; }
        public string PriceUSD { get; set; }
        public SwapToken Token { get; set; }
    }

    public class SwapToken
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Symbol { get; set; }
    }
    
}