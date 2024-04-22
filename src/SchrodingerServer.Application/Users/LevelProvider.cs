using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using SchrodingerServer.AwsS3;
using SchrodingerServer.Common;
using SchrodingerServer.Common.HttpClient;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Dto;
using SchrodingerServer.Options;
using SchrodingerServer.PointServer;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;

namespace SchrodingerServer.Users;

public class LevelProvider : ApplicationService, ILevelProvider
{
    private readonly ILogger<LevelProvider> _logger;
    private readonly IOptionsMonitor<LevelOptions> _levelOptions;
    private readonly IClusterClient _clusterClient;
    private readonly IPointServerProvider _pointServerProvider;
    private readonly IDistributedCache<string> _checkDomainCache;
    private readonly IOptionsMonitor<AccessVerifyOptions> _accessVerifyOptions;
    private readonly IUserInformationProvider _userInformationProvider;
    private readonly Dictionary<string, LevelInfoDto> _levelInfoDic = new ();
    private readonly Dictionary<string, decimal> LevelMinPriceDict = new ();
    private readonly IHttpProvider _httpProvider;
    private readonly AwsS3Client _awsS3Client;

    private static readonly JsonSerializerSettings JsonSerializerSettings = JsonSettingsBuilder.New()
        .IgnoreNullValue()
        .WithCamelCasePropertyNamesResolver()
        .WithAElfTypesConverters()
        .Build();


    public LevelProvider(IClusterClient clusterClient, IPointServerProvider pointServerProvider,
        ILogger<LevelProvider> logger, IDistributedCache<string> checkDomainCache,
        IOptionsMonitor<AccessVerifyOptions> accessVerifyOptions, IUserInformationProvider userInformationProvider,IHttpProvider httpProvider, 
        IOptionsMonitor<LevelOptions> levelOptions, AwsS3Client awsS3Client)
    {
        _clusterClient = clusterClient;
        _logger = logger;
        _httpProvider = httpProvider;
        _levelOptions = levelOptions;
        _awsS3Client = awsS3Client;
    }
    public async Task<List<RankData>> GetItemLevelAsync(GetLevelInfoInputDto input)
    {
        _logger.LogInformation("GetItemLevelAsync param: {param} ", JsonConvert.SerializeObject(input));
        //get rank
        List<RankData> rankDataList;
        try
        {
            var resp = await _httpProvider.InvokeAsync<CatsRankRespDto>(
                _levelOptions.CurrentValue.SchrodingerUrl, PointServerProvider.Api.CatsRank,
                body: JsonConvert.SerializeObject(input, JsonSerializerSettings));
            if (resp is not { Code: "20000" })
            {
                _logger.LogError("CatsRank get failed,response:{response}",(resp == null ? "non result" : resp.Code));
                return new List<RankData>();
            }

            rankDataList = resp.Data; 
        }
        catch (Exception e)
        {
            _logger.LogError("CatsRank get failed",e);
            return new List<RankData>();
        }
        
        //check is in white list
        var address = !string.IsNullOrEmpty(input.Address) ? input.Address : input.SearchAddress;
        var isInWhiteList = await CheckAddressIsInWhiteListAsync(address);
        //get awaken price
        var price = 0.0;
        try
        {
            var resp = await _httpProvider.InvokeAsync<AwakenPriceRespDto>(_levelOptions.CurrentValue.AwakenUrl,
                PointServerProvider.Api.GetAwakenPrice, param: new Dictionary<string, string>
                {
                    ["token0Symbol"] = "ELF",
                    ["token1Symbol"] = "SGR-1",
                    ["feeRate"] = "0.03",
                    ["chainId"] = _levelOptions.CurrentValue.ChainId
                });
            if (resp is not { Code: "20000" })
            {
                _logger.LogError("AwakenPrice get failed,response:{response}",(resp == null ? "non result" : resp.Code));
                return null;
            }

            price = (double)(resp.Data.Items?.First().ValueLocked0 / resp.Data.Items?.First().ValueLocked1);
        }
        catch (Exception e)
        {
            _logger.LogError("AwakenPrice get failed",e);
        }
        //get LevelInfo config 
        foreach (var rankData in rankDataList)
        {
            var levelInfo = await GetItemLevelDicAsync(rankData.Rank.Rank, price);
            if (levelInfo == null) continue;
            if (levelInfo.Level.IsNullOrEmpty())
            {
                levelInfo.Token = "";
                levelInfo.AwakenPrice = "";
            }
            else //use min token price 
            {
                levelInfo.AwakenPrice = (double.Parse(levelInfo.Token) * price).ToString();
            }
            rankData.LevelInfo = levelInfo;
            if (isInWhiteList)
            {
                continue;
            }

            rankData.LevelInfo.Token = "";
            rankData.LevelInfo.AwakenPrice = "";
            rankData.LevelInfo.Level = "";
            rankData.Rank.Rank = 0;
        }
        return rankDataList;
    }

    public async Task<double> GetAwakenSGRPrice()
    {
        //get awaken price
        var price = 0.0;
        try
        {
            var resp = await _httpProvider.InvokeAsync<AwakenPriceRespDto>(_levelOptions.CurrentValue.AwakenUrl,
                PointServerProvider.Api.GetAwakenPrice, param: new Dictionary<string, string>
                {
                    ["token0Symbol"] = "ELF",
                    ["token1Symbol"] = "SGR-1",
                    ["feeRate"] = "0.03",
                    ["chainId"] = _levelOptions.CurrentValue.ChainId
                });
            if (resp is not { Code: "20000" })
            {
                _logger.LogError("AwakenPrice get failed,response:{response}",(resp == null ? "non result" : resp.Code));
                return price;
            }

            price = (double)(resp.Data.Items?.First().ValueLocked0 / resp.Data.Items?.First().ValueLocked1);
        }
        catch (Exception e)
        {
            _logger.LogError("AwakenPrice get failed",e);
        }

        return price;
    }

    public async Task<bool> CheckAddressIsInWhiteListAsync(string address)
    {
        if (string.IsNullOrEmpty(address))
        {
            return false;
        }
        
        var chainId  = _levelOptions.CurrentValue.ChainId;
        var chainIdForReal  = _levelOptions.CurrentValue.ChainIdForReal;
        chainId = chainIdForReal.IsNullOrEmpty() ? chainId : chainIdForReal;
        if (!address.EndsWith(chainId))
        {
            address = "ELF_" + address + "_" + chainId;
        }
        
        try
        {
            var resp = await _httpProvider.InvokeAsync<WhiteListResponse>(_levelOptions.CurrentValue.SchrodingerUrl,
                PointServerProvider.Api.CheckIsInWhiteList, param: new Dictionary<string, string>
                {
                    ["address"] = address
                });
            if (resp is not { Code: "20000" })
            {
                _logger.LogError("CheckAddressIsInWhiteListAsync get failed,response:{response}",(resp == null ? "non result" : resp.Code));
                return false;
            }

            return resp.Data.IsAddressValid;
        }
        catch (Exception e)
        {
            _logger.LogError("CheckAddressIsInWhiteListAsync get failed",e);
        }

        return false;
    }

    public async Task<LevelInfoDto> GetItemLevelDicAsync(int rank, double price)
    {
        var levelInfo = new LevelInfoDto();
        if (_levelInfoDic != null && _levelInfoDic.Count > 0)
        {
            return _levelInfoDic.TryGetValue(rank.ToString(), out levelInfo) ? levelInfo.DeepCopy() : null;
        }
        using (var response = await _awsS3Client.GetObjectAsync(_levelOptions.CurrentValue.S3LevelFileKeyName))
        using (var responseStream = response.ResponseStream)
        using (var reader = new StreamReader(responseStream))
        {
            // Assuming the first line is the header
            reader.ReadLine();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                 var values = line.Split(',');
                // Parse each column to the corresponding property in LevelInfoDto
                var levelInfoDto = new LevelInfoDto
                {
                    // Assuming your CSV columns match LevelInfoDto properties by order
                    SingleProbability = values[1],
                    Items = values[2],
                    Situation = values[3],
                    TotalProbability = values[4],
                    Token = values[5],
                    Classify = values[6],
                    Level = values[7],
                    Grade = values[8],
                    Star = values[9]
                };
                LevelConsts.LevelDescribeDictionary.TryGetValue((levelInfoDto.Level + "-" + levelInfoDto.Classify),
                    out var describe);
                levelInfoDto.Describe = describe;
                levelInfoDto.AwakenPrice = (double.Parse(levelInfoDto.Token) * price).ToString();
                _levelInfoDic[values[0]] = levelInfoDto;

                var token = decimal.Parse(levelInfoDto.Token.Trim());
                if (LevelMinPriceDict.TryGetValue(levelInfoDto.Level, out var minPrice))
                {
                    LevelMinPriceDict[levelInfoDto.Level] = Math.Min(minPrice, token);
                }
                else
                {
                    LevelMinPriceDict[levelInfoDto.Level] = token;
                }
            }
        }
        return _levelInfoDic.TryGetValue(rank.ToString(), out levelInfo) ? levelInfo.DeepCopy() : null;
    }
}