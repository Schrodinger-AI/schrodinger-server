using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using SchrodingerServer.AwsS3;
using SchrodingerServer.Common;
using SchrodingerServer.Common.HttpClient;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Dto;
using SchrodingerServer.Dtos.Cat;
using SchrodingerServer.Dtos.TraitsDto;
using SchrodingerServer.ExceptionHandling;
using SchrodingerServer.Helper;
using SchrodingerServer.Options;
using SchrodingerServer.PointServer;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;

namespace SchrodingerServer.Users;

public class LevelProvider : ApplicationService, ILevelProvider
{
    private readonly ILogger<LevelProvider> _logger;
    private readonly IOptionsMonitor<LevelOptions> _levelOptions;
    private readonly IUserInformationProvider _userInformationProvider;
    private readonly Dictionary<string, LevelInfoDto> _levelInfoDic = new ();
    private readonly Dictionary<string, decimal> LevelMinPriceDict = new ();
    private readonly IHttpProvider _httpProvider;
    private readonly AwsS3Client _awsS3Client;
    private readonly IOptionsMonitor<ActivityTraitOptions> _traitOptions;

    private static readonly JsonSerializerSettings JsonSerializerSettings = JsonSettingsBuilder.New()
        .IgnoreNullValue()
        .WithCamelCasePropertyNamesResolver()
        .WithAElfTypesConverters()
        .Build();
    
    public LevelProvider(
        ILogger<LevelProvider> logger, IHttpProvider httpProvider, 
        IOptionsMonitor<LevelOptions> levelOptions, AwsS3Client awsS3Client, IOptionsMonitor<ActivityTraitOptions> traitOptions)
    {
        _logger = logger;
        _httpProvider = httpProvider;
        _levelOptions = levelOptions;
        _awsS3Client = awsS3Client;
        _traitOptions = traitOptions;
    }
    
    [ExceptionHandler(typeof(Exception), Message = "GetAwakenSGRPrice Failed", ReturnDefault = ReturnDefault.Default, TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    public async Task<double> GetAwakenSGRPrice()
    {
        //get awaken price
        var price = 0.0;
        var resp = await _httpProvider.InvokeAsync<AwakenPriceRespDto>(_levelOptions.CurrentValue.AwakenUrl,
            PointServerProvider.Api.GetAwakenPrice, param: new Dictionary<string, string>
            {
                ["token0Symbol"] = "SGR-1",
                ["token1Symbol"] = "ELF",
                ["feeRate"] = "0.03",
                ["chainId"] = _levelOptions.CurrentValue.ChainId
            });
        if (resp is not { Code: "20000" })
        {
            _logger.LogError("get sgr price failed,response:{response}",(resp == null ? "non result" : resp.Code));
            return price;
        }

        price = (double)(resp.Data.Items?.First().ValueLocked1 / resp.Data.Items?.First().ValueLocked0);

        return price;
    }
    
    [ExceptionHandler(typeof(Exception), Message = "GetAwakenELFPrice Failed", ReturnDefault = ReturnDefault.Default, TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    public async Task<double> GetAwakenELFPrice()
    {
        //get awaken price
        var price = 0.0;
        var resp = await _httpProvider.InvokeAsync<AwakenPriceRespDto>(_levelOptions.CurrentValue.AwakenUrl,
            PointServerProvider.Api.GetAwakenPrice, param: new Dictionary<string, string>
            {
                ["token0Symbol"] = "ELF",
                ["token1Symbol"] = "USDT",
                ["feeRate"] = "0.0005",
                ["chainId"] = _levelOptions.CurrentValue.ChainId
            });
        if (resp is not { Code: "20000" })
        {
            _logger.LogError("get elf price failed, response:{response}",(resp == null ? "non result" : resp.Code));
            return price;
        }

        price = (double)(resp.Data.Items?.First().ValueLocked1 / resp.Data.Items?.First().ValueLocked0);

        return price;
    }

    [ExceptionHandler(typeof(Exception), Message = "Check AddressIsInWhite Failed", ReturnDefault = ReturnDefault.Default, TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    public async Task<bool> CheckAddressIsInWhiteListAsync(string address)
    {
        if (string.IsNullOrEmpty(address))
        {
            return false;
        }
        
        address = FullAddressHelper.ToShortAddress(address);
        
        var resp = await _httpProvider.InvokeAsync<CmsWhiteListResponse>(_levelOptions.CurrentValue.CmsUrl,
            PointServerProvider.Api.CmsWhiteList);
            
        var list = resp.Data.Data.Whitelist;
        return list.Contains(address);
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
    
    [ExceptionHandler(typeof(Exception), Message = "BatchGetForestNftInfoAsync Failed", ReturnDefault = ReturnDefault.New, TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    public async Task<List<NftInfo>> BatchGetForestNftInfoAsync(List<string> nftIdList, string chainId)
    {
        var resp = await _httpProvider.InvokeAsync<BatchGetForestNftInfoDto>(
            _levelOptions.CurrentValue.ForestUrl, PointServerProvider.Api.GetForestInfo,
            body: JsonConvert.SerializeObject(new BatchGetForestNftINfoInput
            {
                CollectionId = chainId + "-SGR-0",
                CollectionType = "nft",
                Sorting = "Low to High",
                ChainList = new List<string> {chainId},
                SkipCount = 0,
                MaxResultCount = nftIdList.Count,
                NFTIdList = nftIdList
            }, JsonSerializerSettings));
        if (resp is not { Code: "20000" })
        {
            _logger.LogError("BatchGetForestNftInfo Error,response:{response}",(resp == null ? "non result" : resp.Code));
            return new List<NftInfo>();
        }

        return resp.Data.Items;
    }
    
    public async Task<RankData> GetRarityInfo(string address, int rank, bool isGen9, bool fullData)
    {
        if (!fullData)
        {
            var isInWhiteList = await CheckAddressIsInWhiteListAsync(address);
            return await GenerateRarity(rank, isGen9, isInWhiteList);
        }
        return await GenerateRarity(rank, isGen9, true);
    }
    
    private async Task<RankData> GenerateRarity(int rank, bool isGen9, bool isInWhiteList)
    {
        var rankData = new RankData
        {
            Rank = new Ranks
            {
                Rank = rank
            },
            LevelInfo =  new LevelInfoDto()
        };

        var price = await GetAwakenSGRPrice();
        var levelInfo = await GetItemLevelDicAsync(rank, price);
            
        if (levelInfo == null)
        {
            if (isGen9)
            {
                rankData.LevelInfo.Describe = "Common,,";
            }
                
            return rankData;
        }
            
        if (levelInfo.Level.IsNullOrEmpty())
        {
            levelInfo.Token = "";
            levelInfo.AwakenPrice = "";
        }
        else if (isInWhiteList) 
        {
            levelInfo.AwakenPrice = (double.Parse(levelInfo.Token) * price).ToString();
        }
            
        rankData.LevelInfo = levelInfo;
        // rankData.LevelInfo.SpecialTrait = input.SpecialTag;
        if (isGen9 && rankData.LevelInfo.Describe.IsNullOrEmpty())
        {
            rankData.LevelInfo.Describe = "Common,,";
        }
            
        if (isInWhiteList)
        {
            return rankData;
        }

        rankData.LevelInfo.Token = "";
        rankData.LevelInfo.AwakenPrice = "";
        rankData.LevelInfo.Level = "";
        rankData.Rank.Rank = 0;

        return rankData;
    }
    
}