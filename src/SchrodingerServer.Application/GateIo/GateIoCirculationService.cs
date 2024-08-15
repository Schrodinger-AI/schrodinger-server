using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SchrodingerServer.Aetherlink;
using SchrodingerServer.Common.Http;
using SchrodingerServer.GateIo.Dtos;
using SchrodingerServer.Options;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.GateIo;

public class GateIoCirculationService : AbpRedisCache, IGateIoCirculationService, ISingletonDependency
{
    private readonly ILogger<GateIoCirculationService> _logger;
    private readonly HttpProvider _httpProvider;
    private readonly IDistributedCacheSerializer _serializer;
    private readonly SgrCirculationOptions _sgrCirculationOptions;
    private readonly IAetherlinkApplicationService _aetherlinkApplicationService;
    private const string SgrCirculationRedisKey = "SgrCirculationRedisKey";
    private const string USDT = "USDT";
    private const string SGR = "SGR-1";
    private const string ELF = "ELF";

    public GateIoCirculationService(IOptions<RedisCacheOptions> optionsAccessor,
        ILogger<GateIoCirculationService> logger, HttpProvider httpProvider, IDistributedCacheSerializer serializer,
        IAetherlinkApplicationService aetherlinkApplicationService,
        IOptionsSnapshot<SgrCirculationOptions> sgrCirculationOptions) : base(optionsAccessor)
    {
        _logger = logger;
        _httpProvider = httpProvider;
        _serializer = serializer;
        _sgrCirculationOptions = sgrCirculationOptions.Value;
        _aetherlinkApplicationService = aetherlinkApplicationService;
    }


    public async Task<long> GetSgrCirculation()
    {
        await ConnectAsync();
        var redisValue = await RedisDatabase.StringGetAsync(SgrCirculationRedisKey);
        if (redisValue.HasValue)
        {
            _logger.LogInformation("get value from cache. value: {value}", redisValue.ToString());
            return _serializer.Deserialize<long>(redisValue);
        }

        try
        {
            var response =
                await _httpProvider.InvokeAsync(HttpMethod.Get, _sgrCirculationOptions.EthApiUrl, param: BuildParams());
            var ethApiResponse = JsonConvert.DeserializeObject<EthApiResponse>(response);

            if (ethApiResponse.Status != EthApiResponseConstant.SuccessStatus ||
                ethApiResponse.Message != EthApiResponseConstant.SuccessMessage)
            {
                _logger.LogError("get eth api fail. response: {response}", response);
                throw new Exception("get sgr circulation fail.");
            }

            var result = long.Parse(_sgrCirculationOptions.TotalSupply) -
                         long.Parse(ethApiResponse.Result) / (long)Math.Pow(10, 8) -
                         long.Parse(_sgrCirculationOptions.Surplus) - 
                         long.Parse(_sgrCirculationOptions.AelfSideChainBalance);

            await RedisDatabase.StringSetAsync(SgrCirculationRedisKey, _serializer.Serialize(result),
                new TimeSpan(0, 0, _sgrCirculationOptions.CacheExpiredTtl));
            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "get eth api fail.");
            throw new Exception("get sgr circulation fail.");
        }
    }

    private Dictionary<string, string> BuildParams()
    {
        var param = new Dictionary<string, string>();
        param["module"] = "account";
        param["action"] = "tokenbalance";
        param["tag"] = "latest";
        param["contractaddress"] = _sgrCirculationOptions.SgrContractAddress;
        param["address"] = _sgrCirculationOptions.AccountAddress;
        param["apikey"] = _sgrCirculationOptions.EthApiKey;
        return param;
    }

    public async Task<decimal> GetSgrPrice()
    {
        // var elfPriceDto = await _awakenLiquidityProvider.GetPriceAsync(ELF, USDT, "tDVV", "0.0005");
        // var elfPrice = elfPriceDto.Items.FirstOrDefault().Price;
        // AssertHelper.IsTrue(elfPrice != null && elfPrice > 0, "ELF price is null or zero");
        //
        // var sgrPriceInElfDto = await _awakenLiquidityProvider.GetPriceAsync(SGR, ELF,"tDVV", "0.03");
        // var sgrPriceInElf = sgrPriceInElfDto.Items.FirstOrDefault().Price;
        // AssertHelper.IsTrue(sgrPriceInElf != null && sgrPriceInElf > 0, "SGR price is null or zero");
        
        return await _aetherlinkApplicationService.GetTokenPriceInUsdt("sgr");;
    }
}