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
using SchrodingerServer.Dtos.Cat;
using SchrodingerServer.Dtos.TraitsDto;
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
    
    public async Task<List<RankData>> GetItemLevelAsync(GetLevelInfoInputDto input)
    {
        _logger.LogInformation("GetItemLevelAsync param: {param} ", JsonConvert.SerializeObject(input));
        
        var catsTraits = input.CatsTraits;
        foreach (var catTraits in catsTraits)
        {
            var gen1Traits = catTraits.FirstOrDefault();
            var gen2To9Traits = catTraits.LastOrDefault();
            
            var totalTraits = gen1Traits.Zip(gen2To9Traits, (a, b) =>
            {
                var x = new List<string>(a);
                x.AddRange(b);
                return x;
            }).ToList();

            var traitTypes = totalTraits.FirstOrDefault();
            var traitValues = totalTraits.LastOrDefault();
            var traitInfo = traitTypes.Zip(traitValues, (a, b) => new TraitsInfo
            {
                TraitType = a,
                Value = b
            }).ToList();
            
            input.SpecialTag = TraitHelper.GetSpecialTraitOfElection(_traitOptions.CurrentValue, traitInfo);
            input.IsGen9 = traitValues.Count >= 11;
            
            var newGen1Values = TraitHelper.ReplaceTraitValues(_traitOptions.CurrentValue, gen1Traits.FirstOrDefault(), gen1Traits.LastOrDefault());
            if (!newGen1Values.SequenceEqual(gen1Traits[1]))
            {
                _logger.LogInformation("gen 1 trait different, new trait: {param} ", JsonConvert.SerializeObject(newGen1Values));
            }
            gen1Traits[1] = newGen1Values;
            
            
            var newGen2To9Values = TraitHelper.ReplaceTraitValues(_traitOptions.CurrentValue, gen2To9Traits.FirstOrDefault(), gen2To9Traits.LastOrDefault());
            if (!newGen2To9Values.SequenceEqual(gen2To9Traits[1]))
            {
                _logger.LogInformation("gen 2to9 trait different, new trait: {param} ", JsonConvert.SerializeObject(newGen2To9Values));
            }
            gen2To9Traits[1] = newGen2To9Values;
        }
        
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
                    ["token0Symbol"] = "SGR-1",
                    ["token1Symbol"] = "ELF",
                    ["feeRate"] = "0.03",
                    ["chainId"] = _levelOptions.CurrentValue.ChainId
                });
            if (resp is not { Code: "20000" })
            {
                _logger.LogError("AwakenPrice get failed,response:{response}",(resp == null ? "non result" : resp.Code));
                return null;
            }

            price = (double)(resp.Data.Items?.First().ValueLocked1 / resp.Data.Items?.First().ValueLocked0);
        }
        catch (Exception e)
        {
            _logger.LogError("AwakenPrice get failed",e);
        }
        //get LevelInfo config 
        foreach (var rankData in rankDataList)
        {
            var levelInfo = await GetItemLevelDicAsync(rankData.Rank.Rank, price);
            
            if (levelInfo == null)
            {
                rankData.LevelInfo = new LevelInfoDto
                {
                    SpecialTrait = input.SpecialTag
                };
                
                if (input.IsGen9)
                {
                    rankData.LevelInfo.Describe = "Common,,";
                }
                
                continue;
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
            rankData.LevelInfo.SpecialTrait = input.SpecialTag;
            if (input.IsGen9 && rankData.LevelInfo.Describe.IsNullOrEmpty())
            {
                rankData.LevelInfo.Describe = "Common,,";
            }
            
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
                    ["token0Symbol"] = "SGR-1",
                    ["token1Symbol"] = "ELF",
                    ["feeRate"] = "0.03",
                    ["chainId"] = _levelOptions.CurrentValue.ChainId
                });
            if (resp is not { Code: "20000" })
            {
                _logger.LogError("AwakenPrice get failed,response:{response}",(resp == null ? "non result" : resp.Code));
                return price;
            }

            price = (double)(resp.Data.Items?.First().ValueLocked1 / resp.Data.Items?.First().ValueLocked0);
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

    public async Task<List<NftInfo>> BatchGetForestNftInfoAsync(List<string> nftIdList, string chainId)
    {
        try
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
        catch (Exception e)
        {
            _logger.LogError("BatchGetForestNftInfoAsync Failed, {msg}", e);
            return new List<NftInfo>();
        }
    }
    
}