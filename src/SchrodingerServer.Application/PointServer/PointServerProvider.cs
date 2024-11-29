using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AElf;
using AElf.ExceptionHandler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Dtos;
using SchrodingerServer.Common.HttpClient;
using SchrodingerServer.Common.Options;
using SchrodingerServer.ExceptionHandling;
using SchrodingerServer.PointServer.Dto;
using SchrodingerServer.Users.Dto;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.PointServer;

public interface IPointServerProvider
{
    Task<bool> CheckDomainAsync(string domain);

    Task<MyPointDetailsDto> GetMyPointsAsync(GetMyPointsInput input);
    
    Task<EcoEarnRewardDto> GetEcoEarnRewardsAsync(string address);
    
    Task<EcoEarnTotalRewardDto> GetEcoEarnTotalRewardsAsync(string address);
}

public class PointServerProvider : IPointServerProvider, ISingletonDependency
{
    public static class Api
    {
        public static ApiInfo CheckDomain = new(HttpMethod.Post, "/api/app/apply/domain/check");
        public static ApiInfo GetMyPoints = new(HttpMethod.Get, "/api/app/points/my/points");
        public static ApiInfo CatsRank = new(HttpMethod.Post, "/api/probability/catsRank");
        public static ApiInfo GetAwakenPrice = new(HttpMethod.Get, "/api/app/trade-pairs");
        public static ApiInfo CheckIsInWhiteList = new(HttpMethod.Get, "/api/probability/isAddressValid");
        public static ApiInfo GetMyRewards = new(HttpMethod.Post, "/api/app/points/staking/rewards/info");
        public static ApiInfo GetAwakenTradeRecords = new(HttpMethod.Get, "/api/app/trade-records");
        public static ApiInfo GetForestInfo = new(HttpMethod.Post, "/api/app/nft/composite-nft-infos");
        public static ApiInfo GetTotalReward = new(HttpMethod.Post, "/api/app/points/staking/rewards/total");
        public static ApiInfo CmsWhiteList = new(HttpMethod.Get, "/cms/items/whitelist");
    }

    private readonly ILogger<PointServerProvider> _logger;
    private readonly IOptionsMonitor<PointServiceOptions> _pointServiceOptions;
    private readonly IHttpProvider _httpProvider;

    private static readonly JsonSerializerSettings JsonSerializerSettings = JsonSettingsBuilder.New()
        .IgnoreNullValue()
        .WithCamelCasePropertyNamesResolver()
        .WithAElfTypesConverters()
        .Build();


    public PointServerProvider(IHttpProvider httpProvider, IOptionsMonitor<PointServiceOptions> pointServiceOptions, ILogger<PointServerProvider> logger)
    {
        _httpProvider = httpProvider;
        _pointServiceOptions = pointServiceOptions;
        _logger = logger;
    }

    [ExceptionHandler(typeof(Exception), Message = "CheckDomainAsync error", ReturnDefault = ReturnDefault.Default, TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    public async Task<bool> CheckDomainAsync(string domain)
    {
        var resp = await _httpProvider.InvokeAsync<CommonResponseDto<CheckDomainResponse>>(
            _pointServiceOptions.CurrentValue.BaseUrl, Api.CheckDomain,
            body: JsonConvert.SerializeObject(new CheckDomainRequest
            {
                Domain = domain
            }, JsonSerializerSettings));
        AssertHelper.NotNull(resp, "Response empty");
        AssertHelper.NotNull(resp.Success, "Response failed, {}", resp.Message);
        return resp.Data.Exists;
    }
    
    [ExceptionHandler(typeof(Exception), Message = "GetMyPointsAsync error", ReturnDefault = ReturnDefault.New, TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    public async Task<MyPointDetailsDto> GetMyPointsAsync(GetMyPointsInput input)
    {
        var resp = await _httpProvider.InvokeAsync<CommonResponseDto<MyPointDetailsDto>>(
            _pointServiceOptions.CurrentValue.BaseUrl, Api.GetMyPoints, null,
            new Dictionary<string, string>()
            {
                ["dappname"] = _pointServiceOptions.CurrentValue.DappId,
                ["address"] = input.Address,
                ["domain"] = input.Domain
            });
        AssertHelper.NotNull(resp, "Response empty");
        AssertHelper.NotNull(resp.Success, "Response failed, {}", resp.Message);
        return resp.Data ?? new MyPointDetailsDto();
    }


    public string GetSign(object obj)
    {
        var source = ObjectHelper.ConvertObjectToSortedString(obj, "Signature");
        source += _pointServiceOptions.CurrentValue.DappSecret;
        return HashHelper.ComputeFrom(source).ToHex();
    }
    
    [ExceptionHandler(typeof(Exception), Message = "GetEcoEarnRewardsAsync error", ReturnDefault = ReturnDefault.New, TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    public async Task<EcoEarnRewardDto> GetEcoEarnRewardsAsync(string address)
    {
        if (!_pointServiceOptions.CurrentValue.EcoEarnReady)
        {
            _logger.LogInformation("EcoEarnRewards is not ready");
            return new EcoEarnRewardDto
            {
                Reward = new Dictionary<string, string>()
            };
        }
        
        var resp = await _httpProvider.InvokeAsync<CommonResponseDto<EcoEarnRewardDto>>(
            _pointServiceOptions.CurrentValue.EcoEarnUrl, Api.GetMyRewards, body: JsonConvert.SerializeObject(new GetEcoEarnRewardRequest()
            {
                Address = address,
                DappId = _pointServiceOptions.CurrentValue.DappId
            })
        );
        AssertHelper.NotNull(resp, "Response empty");
        AssertHelper.NotNull(resp.Success, "Response failed, {}", resp.Message);
        return resp.Data ?? new EcoEarnRewardDto();
    }
    
    [ExceptionHandler(typeof(Exception), Message = "GetEcoEarnTotalRewardsAsync error", ReturnDefault = ReturnDefault.New, TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    public async Task<EcoEarnTotalRewardDto> GetEcoEarnTotalRewardsAsync(string address)
    {
        if (!_pointServiceOptions.CurrentValue.EcoEarnReady)
        {
            _logger.LogInformation("EcoEarnTotalRewards is not ready");
            return new EcoEarnTotalRewardDto();
        }
        
        var resp = await _httpProvider.InvokeAsync<CommonResponseDto<EcoEarnTotalRewardDto>>(
            _pointServiceOptions.CurrentValue.EcoEarnUrl, Api.GetTotalReward, body: JsonConvert.SerializeObject(new GetEcoEarnRewardRequest()
            {
                Address = address,
                DappId = _pointServiceOptions.CurrentValue.DappId
            })
        );
        AssertHelper.NotNull(resp, "Response empty");
        AssertHelper.NotNull(resp.Success, "Response failed, {}", resp.Message);
        return resp.Data ?? new EcoEarnTotalRewardDto();
    }
}