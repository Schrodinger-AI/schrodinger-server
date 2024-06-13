using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SchrodingerServer.Activity.Eto;
using SchrodingerServer.AddressRelationship.Dto;
using SchrodingerServer.Adopts.provider;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Users;
using SchrodingerServer.Users.Index;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Activity;

public class ActivityApplicationService : ApplicationService, IActivityApplicationService
{
    private readonly IAddressRelationshipProvider _addressRelationshipProvider;
    private readonly ILogger<ActivityApplicationService> _logger;
    private IOptionsMonitor<ActivityOptions> _activityOptions;
    private IOptionsMonitor<ActivityRankOptions> _activityRankOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IDistributedCache<string> _distributedCache;
    private readonly IAdoptGraphQLProvider _adoptGraphQlProvider;
    private readonly IPortkeyProvider _portkeyProvider;
    
    public ActivityApplicationService(
        IAddressRelationshipProvider addressRelationshipProvider, 
        ILogger<ActivityApplicationService> logger, 
        IOptionsMonitor<ActivityOptions> activityOptions, 
        IObjectMapper objectMapper, 
        IDistributedEventBus distributedEventBus,
        IAdoptGraphQLProvider adoptGraphQlProvider, 
        IPortkeyProvider portkeyProvider,
        IDistributedCache<string> distributedCache,
        IOptionsMonitor<ActivityRankOptions> activityRankOptions)
    {
        _addressRelationshipProvider = addressRelationshipProvider;
        _logger = logger;
        _activityOptions = activityOptions;
        _objectMapper = objectMapper;
        _distributedEventBus = distributedEventBus;
        _adoptGraphQlProvider = adoptGraphQlProvider;
        _portkeyProvider = portkeyProvider;
        _distributedCache = distributedCache;
        _activityRankOptions = activityRankOptions;
    }

    public async Task<ActivityListDto>GetActivityListAsync(GetActivityListInput input)
    {
        var activityOptions = _activityOptions.CurrentValue;

        var activityList = activityOptions.ActivityList.Where(a => a.IsShow).ToList();
        if (activityList.Count == 0)
        {
            return new ActivityListDto();
        }
        
        var activityDtoList = _objectMapper.Map<List<ActivityInfo>, List<ActivityDto>>(activityList).Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        
        activityDtoList.ForEach(activity =>
        {
            var beginTime = DateTimeOffset.FromUnixTimeMilliseconds(activity.BeginTime).UtcDateTime;
            var timeDiff = DateTime.UtcNow - beginTime;
            if (timeDiff.TotalSeconds > activityOptions.NewTagInterval)
            {
                activity.IsNew = false;
            }

            activity.LinkUrl = activity.LinkUrl + "?activityId=" + activity.ActivityId;
        });

        var res = new ActivityListDto
        {
            TotalCount = activityList.Count,
            Items = activityDtoList.ToList()
        };

        return res;
    }

    public async Task<ActivityInfoDto> GetActivityInfoAsync()
    {
        var activityOptions = _activityOptions.CurrentValue;

        var hasNewActivity = activityOptions.ActivityList.Any(activity =>
        {
            var beginTime = DateTimeOffset.FromUnixTimeMilliseconds(activity.BeginTime).UtcDateTime;
            var timeDiff = DateTime.UtcNow - beginTime;
            return timeDiff.TotalSeconds < activityOptions.NewTagInterval;
        });
        
        return new ActivityInfoDto
        {
            HasNewActivity = hasNewActivity
        };
    }

    public async Task  BindActivityAddressAsync(BindActivityAddressInput input)
    {
        _logger.LogInformation("BindActivityAddress input:{input}", JsonConvert.SerializeObject(input));
        var publicKeyVal = input.PublicKey;
        var signatureVal = input.Signature;
        var aelfAddress = input.AelfAddress;
        var sourceChainAddress = input.SourceChainAddress;
        var activityId = input.ActivityId;
        
        var signature = ByteArrayHelper.HexStringToByteArray(signatureVal);
        
        AssertHelper.IsTrue(CryptoHelper.RecoverPublicKey(signature,
            HashHelper.ComputeFrom(string.Join("-", aelfAddress, sourceChainAddress, activityId)).ToByteArray(),
            out var managerPublicKey), "Invalid signature.");
        AssertHelper.IsTrue(managerPublicKey.ToHex() == publicKeyVal, "Invalid publicKey or signature.");
        
        var hasBind = await _addressRelationshipProvider.CheckActivityBindingExistsAsync(aelfAddress, sourceChainAddress, activityId);
        if (hasBind)
        {
            _logger.LogError("Binding already exists for aelfAddress:{aelfAddress}, sourceChainAddress:{sourceChainAddress}, activityId:{activityId}", aelfAddress, sourceChainAddress, activityId);
            throw new UserFriendlyException("This EVM address has been bound to an aelf address and cannot be bound again");
        }
        
        await  _addressRelationshipProvider.BindActivityAddressAsync(aelfAddress, sourceChainAddress, ChainType.EVM, activityId);

        _logger.LogInformation("BindActivityAddress finished");
    }

    public async Task<ActivityAddressDto> GetActivityAddressAsync(GetActivityAddressInput input)
    {
        _logger.LogInformation("GetActivityAddress input:{input}", JsonConvert.SerializeObject(input));
        var res = new  ActivityAddressDto
        {
            SourceChainAddress = ""
        };
        var activityAddressIndex = await _addressRelationshipProvider.GetActivityAddressAsync(input.AelfAddress, input.ActivityId);

        if (activityAddressIndex != null && !activityAddressIndex.Id.IsNullOrEmpty())
        {
            res.SourceChainAddress = activityAddressIndex.SourceChainAddress;
        }

        return res;
    }
    
    public async Task<RankDto> GetRankAsync(GetRankInput input)
    {
        _logger.LogInformation("GetRank input:{input}", JsonConvert.SerializeObject(input));

        var rankOptions = _activityRankOptions.CurrentValue;
        var beginTime = rankOptions.BeginTime;
        var endTime = rankOptions.EndTime;
        
        var cur = TimeHelper.GetTimeStampInSeconds();

        if (cur < endTime)
        {
            endTime = cur;
        }
        
        if (input.UpdateAddressCache)
        {
            await _distributedEventBus.PublishAsync(new UpdateAddressCacheEto
            {
                BeginTime = beginTime,
                EndTime = endTime
            });

            return new RankDto();
        }

        var res = await _adoptGraphQlProvider.GetAdoptInfoByTime(beginTime, endTime);
        if (res.IsNullOrEmpty())
        {
            _logger.LogInformation("GetRank empty");
            return new RankDto();
        }
        
        var rankDataDict = new Dictionary<string, long>();
        foreach (var adopt in res)
        {
            var address = adopt.Adopter;
            if (address.IsNullOrEmpty())
            {
                continue;
            }
            var amount = adopt.InputAmount;
            rankDataDict[address] = rankDataDict.TryGetValue(address, out var value) ? value + amount : amount;
        }
        
        var rankDataList = rankDataDict.Select(kvp => 
                new ActivityRankData
                {
                    Address = kvp.Key, 
                    Scores = (long) (kvp.Value * 1314 / Math.Pow(10, 8))
                })
            .OrderByDescending(rd => rd.Scores)
            .ToList();

        var header = new List<RankHeader>(rankOptions.Header);
        if (!input.IsFinal)
        {
            header.RemoveAt(header.Count-1);
        }
        
        var result = new RankDto
        {
            Data = new  List<ActivityRankData>(),
            Header = header
        };

        var displayNumbers = input.IsFinal ? rankOptions.FinalDisplayNumber : rankOptions.NormalDisplayNumber;
        var rank = 0;
        foreach (var rankData in rankDataList)
        {
            var address = rankData.Address;
            var key = IdGenerateHelper.GetEOAAddressCacheKey(address);
            var cache = await _distributedCache.GetAsync(key);
            var isEoa = false;
            if (cache == null)
            {
                var response = await _portkeyProvider.IsEOAAddress(address);
                if (response != null)
                {
                    isEoa = response.IsEOAAddress;
                    _logger.LogInformation("{address} is EOA Address: {isEoa}", address, isEoa);
                    await _distributedCache.SetAsync(key, isEoa.ToString(),  new DistributedCacheEntryOptions()
                    {
                        SlidingExpiration = TimeSpan.FromDays(300)
                    });
                }
                else
                {
                    _logger.LogError("IsEOAAddress judgment for {address} failed", address);
                }
            }
            else
            {
                isEoa = bool.Parse(cache);
            }
            
            if (!isEoa)
            {
                rank++;
                if (input.IsFinal)
                {
                    var reward = GetRankReward(rank);
                    rankData.Reward = reward.ToString();
                }
                result.Data.Add(rankData);
            }

            if (rank >= displayNumbers)
            {
                break;
            }
        }
        
        return result;
    }

    
    private long GetRankReward(int rank)
    {
        var rankRewards = _activityRankOptions.CurrentValue.RankRewards;
        foreach (var rankReward in rankRewards)
        {
            if (rank <= rankReward.Rank)
            {
                return rankReward.Reward;
            }
        }

        return 0;
    }
}