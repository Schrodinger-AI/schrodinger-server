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
        
        var inprogressStageTime = GetInProgressStageTime();
        var beginTime = TimeHelper.ToUtcSeconds(inprogressStageTime.StartTime);
        var endTime = TimeHelper.ToUtcSeconds(inprogressStageTime.EndTime);
        
        _logger.LogInformation("GetRank inprogress stage time, being:{begin}, end:{end}", beginTime, endTime);
        
        var cur = TimeHelper.GetTimeStampInSeconds();

        if (cur < endTime)
        {
            endTime = cur;
        }
        
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
        
        if (input.UpdateAddressCache)
        {
            await _distributedEventBus.PublishAsync(new UpdateAddressCacheEto
            {
                BeginTime = beginTime,
                EndTime = endTime
            });

            return result;
        }

        var res = await _adoptGraphQlProvider.GetAdoptInfoByTime(beginTime, endTime);
        if (res.IsNullOrEmpty())
        {
            _logger.LogInformation("GetRank empty");
            return result;
        }
        
        var rankDataDict = new Dictionary<string, RankItem>();
        foreach (var adopt in res)
        {
            var address = adopt.Adopter;
            if (address.IsNullOrEmpty())
            {
                continue;
            }
            var amount = adopt.InputAmount;
            var adoptTime = adopt.AdoptTime;

            // var value;
            var exist = rankDataDict.TryGetValue(address, out var value);
            if (exist)
            {
                var totalAmount = value.TotalAmount + amount;
                var updateTime = value.UpdateTime > adoptTime ? value.UpdateTime : adoptTime;
                rankDataDict[address].TotalAmount = totalAmount;
                rankDataDict[address].UpdateTime = updateTime;
            }
            else
            {
                rankDataDict[address] = new RankItem
                {
                    TotalAmount = amount,
                    UpdateTime = adoptTime
                };
            }
        }
        
        var rankDataList = rankDataDict.Select(kvp => 
                new ActivityRankData
                {
                    Address = kvp.Key, 
                    Scores = (long) (kvp.Value.TotalAmount * 1314 / Math.Pow(10, 8)),
                    UpdateTime = kvp.Value.UpdateTime
                })
            .ToList();
        
        rankDataList.Sort((item1, item2) =>
        { 
            int scoreComparison = item2.Scores.CompareTo(item1.Scores);
            if (scoreComparison != 0)
            {
                return scoreComparison;
            }

            int timeComparison = item1.UpdateTime.CompareTo(item2.UpdateTime);
            if (timeComparison != 0)
            {
                return timeComparison;
            }

            return item1.Address.CompareTo(item2.Address); 
        });

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

    public async Task<StageDto> GetStageAsync()
    {
        var inprogressStageTime = GetInProgressStageTime();
        var displayedStageTime = GetDisplayedStageTime();
        return new StageDto
        {
            InProgress = new StageTime
            {
                StartTime = TimeHelper.ToUtcMilliSeconds(inprogressStageTime.StartTime),
                EndTime = TimeHelper.ToUtcMilliSeconds(inprogressStageTime.EndTime)
            },
            Displayed = new StageTime
            {
                StartTime = TimeHelper.ToUtcMilliSeconds(displayedStageTime.StartTime),
                EndTime = TimeHelper.ToUtcMilliSeconds(displayedStageTime.EndTime)
            },
        };
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

    private StageTimeInDateTime GetInProgressStageTime()
    {
        DateTime today = DateTime.UtcNow;

        DateTime startTime, endTime;

        // Check if the current time is on or before this Wednesday 23:59:59 UTC
        if (today.DayOfWeek < DayOfWeek.Wednesday || (today.DayOfWeek == DayOfWeek.Wednesday && today.TimeOfDay <= new TimeSpan(23, 59, 59)))
        {
            // Set startTime to last Thursday 00:00:00 UTC
            startTime = today.Date.AddDays(DayOfWeek.Thursday - today.DayOfWeek).AddDays(-7);
            // Set endTime to this Tuesday 23:59:59 UTC
            endTime = today.Date.AddDays(DayOfWeek.Tuesday - today.DayOfWeek).Add(new TimeSpan(23, 59, 59));
        }
        else
        {
            // Set startTime to this Thursday 00:00:00 UTC
            startTime = today.Date.AddDays(DayOfWeek.Thursday - today.DayOfWeek);
            // Set endTime to next Tuesday 23:59:59 UTC
            endTime = today.Date.AddDays(DayOfWeek.Tuesday - today.DayOfWeek + 7).Add(new TimeSpan(23, 59, 59));
        }
        
        return  new StageTimeInDateTime
        {
            StartTime = startTime,
            EndTime = endTime
        };
    }
    
    
    private StageTimeInDateTime GetDisplayedStageTime()
    {
        DateTime today = DateTime.UtcNow;

        DateTime startTime, endTime;

        // Check if the current time is on or before Wednesday 23:59:59 UTC
        if (today.DayOfWeek < DayOfWeek.Wednesday || (today.DayOfWeek == DayOfWeek.Wednesday && today.TimeOfDay <= new TimeSpan(23, 59, 59)))
        {
            // Set startTime to this Wednesday 00:00:00 UTC
            startTime = today.Date.AddDays(DayOfWeek.Wednesday - today.DayOfWeek);
            // Set startTime to this Wednesday 23:59:59 UTC
            endTime = today.Date.AddDays(DayOfWeek.Wednesday - today.DayOfWeek).Add(new TimeSpan(23, 59, 59));
        }
        else
        {
            // Set startTime to next Wednesday 00:00:00 UTC
            startTime = today.Date.AddDays(DayOfWeek.Wednesday - today.DayOfWeek + 7);
            // Set startTime to next Wednesday 00:00:00 UTC
            endTime = today.Date.AddDays(DayOfWeek.Wednesday - today.DayOfWeek + 7).Add(new TimeSpan(23, 59, 59));
        }
        
        return  new StageTimeInDateTime
        {
            StartTime = startTime,
            EndTime = endTime
        };
    }
}