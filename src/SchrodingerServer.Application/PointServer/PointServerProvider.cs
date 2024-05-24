using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AElf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Dtos;
using SchrodingerServer.Common.HttpClient;
using SchrodingerServer.Common.Options;
using SchrodingerServer.PointServer.Dto;
using SchrodingerServer.Users.Dto;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.PointServer;

public interface IPointServerProvider
{
    Task<bool> CheckDomainAsync(string domain);

    Task<MyPointDetailsDto> GetMyPointsAsync(GetMyPointsInput input);
    
    Task<EcoEarnRewardDto> GetEcoEarnRewardsAsync(string address);
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

    public async Task<bool> CheckDomainAsync(string domain)
    {
        try
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
        catch (Exception e)
        {
            _logger.LogWarning(e, "Points domain check failed");
            return false;
        }
    }

    public async Task<MyPointDetailsDto> GetMyPointsAsync(GetMyPointsInput input)
    {
        try
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
        catch (Exception e)
        {
            _logger.LogWarning(e, "Points domain get points failed");
            return new MyPointDetailsDto();
        }
    }


    public string GetSign(object obj)
    {
        var source = ObjectHelper.ConvertObjectToSortedString(obj, "Signature");
        source += _pointServiceOptions.CurrentValue.DappSecret;
        return HashHelper.ComputeFrom(source).ToHex();
    }

    public async Task<EcoEarnRewardDto> GetEcoEarnRewardsAsync(string address)
    {
        try
        {
            var resp = await _httpProvider.InvokeAsync<CommonResponseDto<EcoEarnRewardDto>>(
                _pointServiceOptions.CurrentValue.EcoEarnUrl, Api.GetMyRewards, body: JsonConvert.SerializeObject(new GetEcoEarnRewardRequest()
                {
                    Address = address
                })
                );
            AssertHelper.NotNull(resp, "Response empty");
            AssertHelper.NotNull(resp.Success, "Response failed, {}", resp.Message);
            return resp.Data ?? new EcoEarnRewardDto();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Points domain get points failed");
            return new EcoEarnRewardDto();
        }
    }
}