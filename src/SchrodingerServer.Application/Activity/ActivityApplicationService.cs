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
using SchrodingerServer.Awaken.Provider;
using SchrodingerServer.Cat.Provider;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Dtos.Cat;
using SchrodingerServer.Options;
using SchrodingerServer.Users;
using SchrodingerServer.Users.Index;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;
using RankItem = SchrodingerServer.AddressRelationship.Dto.RankItem;

namespace SchrodingerServer.Activity;

public class ActivityApplicationService : ApplicationService, IActivityApplicationService
{
    private readonly IAddressRelationshipProvider _addressRelationshipProvider;
    private readonly ILogger<ActivityApplicationService> _logger;
    private IOptionsMonitor<ActivityOptions> _activityOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IDistributedCache<string> _distributedCache;
    private readonly IAdoptGraphQLProvider _adoptGraphQlProvider;
    private readonly IPortkeyProvider _portkeyProvider;
    private readonly ISchrodingerCatProvider _schrodingerCatProvider;
    private readonly IAwakenLiquidityProvider _awakenLiquidityProvider;
    private readonly IOptionsMonitor<LevelOptions> _levelOptions;
    
    public ActivityApplicationService(
        IAddressRelationshipProvider addressRelationshipProvider, 
        ILogger<ActivityApplicationService> logger, 
        IOptionsMonitor<ActivityOptions> activityOptions, 
        IObjectMapper objectMapper, 
        IDistributedEventBus distributedEventBus,
        IAdoptGraphQLProvider adoptGraphQlProvider, 
        IPortkeyProvider portkeyProvider,
        IDistributedCache<string> distributedCache,
        ISchrodingerCatProvider schrodingerCatProvider,
        IAwakenLiquidityProvider awakenLiquidityProvider,
        IOptionsMonitor<LevelOptions> levelOptions)
    {
        _addressRelationshipProvider = addressRelationshipProvider;
        _logger = logger;
        _activityOptions = activityOptions;
        _objectMapper = objectMapper;
        _distributedEventBus = distributedEventBus;
        _adoptGraphQlProvider = adoptGraphQlProvider;
        _portkeyProvider = portkeyProvider;
        _distributedCache = distributedCache;
        _schrodingerCatProvider = schrodingerCatProvider;
        _awakenLiquidityProvider = awakenLiquidityProvider;
        _levelOptions = levelOptions;
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

        var rankOptions = GetRankOptions(input.ActivityId);
        
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
        
        var rankDataList = await GetRankListAsync(input.ActivityId);

        var displayNumbers = input.IsFinal ? rankOptions.FinalDisplayNumber : rankOptions.NormalDisplayNumber;
        var rank = 0;
        foreach (var rankData in rankDataList)
        {
            var address = rankData.Address;

            if (input.ActivityId == ActivityConstants.SGR5RankActivityId)
            {
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
                        var reward = GetRankReward(rank, rankOptions);
                        rankData.Reward = reward.ToString();
                    }
                    result.Data.Add(rankData);
                }
            }
            else
            {
                rank++;
                if (input.IsFinal)
                {
                    var reward = GetRankReward(rank, rankOptions);
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

    public async Task<StageDto> GetStageAsync(string activityId)
    {
        var inprogressStageTime = GetInProgressStageTime(activityId);
        var displayedStageTime = GetDisplayedStageTime(activityId);
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
    
    private long GetRankReward(int rank, ActivityRankOptions rankOptions)
    {
        var rankRewards = rankOptions.RankRewards;
        foreach (var rankReward in rankRewards)
        {
            if (rank <= rankReward.Rank)
            {
                return rankReward.Reward;
            }
        }

        return 0;
    }

    private StageTimeInDateTime GetInProgressStageTime(string activityId)
    {
        switch (activityId)
        {
            case ActivityConstants.SGR5RankActivityId:
                return GetInProgressStageTimeForSGR5();
            case ActivityConstants.SGR7RankActivityId:
                return GetInProgressStageTimeForSGR7();
            default:
                return GetInProgressStageTimeForSGR5();
        }
    }

    private StageTimeInDateTime GetInProgressStageTimeForSGR5()
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
    
    private StageTimeInDateTime GetInProgressStageTimeForSGR7()
    {
        DateTime today = DateTime.UtcNow;

        DateTime startTime, endTime;

        // Check if the current time is on or before this Thursday 23:59:59 UTC
        if (today.DayOfWeek < DayOfWeek.Thursday || (today.DayOfWeek == DayOfWeek.Thursday && today.TimeOfDay <= new TimeSpan(23, 59, 59)))
        {
            // Set startTime to last Friday 00:00:00 UTC
            startTime = today.Date.AddDays(DayOfWeek.Friday - today.DayOfWeek).AddDays(-7);
            // Set endTime to this Wednesday 23:59:59 UTC
            endTime = today.Date.AddDays(DayOfWeek.Wednesday - today.DayOfWeek).Add(new TimeSpan(23, 59, 59));
        }
        else
        {
            // Set startTime to this Friday 00:00:00 UTC
            startTime = today.Date.AddDays(DayOfWeek.Friday - today.DayOfWeek);
            // Set endTime to next Wednesday 23:59:59 UTC
            endTime = today.Date.AddDays(DayOfWeek.Wednesday - today.DayOfWeek + 7).Add(new TimeSpan(23, 59, 59));
        }
        
        return  new StageTimeInDateTime
        {
            StartTime = new DateTime(2024, 7, 12, 0, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2024, 7, 17, 23, 59, 59, DateTimeKind.Utc)
        };
    }
    
    
    private StageTimeInDateTime GetDisplayedStageTime(string activityId)
    {
        switch (activityId)
        {
            case ActivityConstants.SGR5RankActivityId:
                return GetDisplayedStageTimeForSGR5();
            case ActivityConstants.SGR7RankActivityId:
                return GetDisplayedStageTimeForSGR7();
            default:
                return GetDisplayedStageTimeForSGR5();
        }
    }
    
    private StageTimeInDateTime GetDisplayedStageTimeForSGR5()
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
    
    private StageTimeInDateTime GetDisplayedStageTimeForSGR7()
    {
        DateTime today = DateTime.UtcNow;

        DateTime startTime, endTime;

        // Check if the current time is on or before Thursday 23:59:59 UTC
        if (today.DayOfWeek < DayOfWeek.Thursday || (today.DayOfWeek == DayOfWeek.Thursday && today.TimeOfDay <= new TimeSpan(23, 59, 59)))
        {
            // Set startTime to this Thursday 00:00:00 UTC
            startTime = today.Date.AddDays(DayOfWeek.Thursday - today.DayOfWeek);
            // Set startTime to this Thursday 23:59:59 UTC
            endTime = today.Date.AddDays(DayOfWeek.Thursday - today.DayOfWeek).Add(new TimeSpan(23, 59, 59));
        }
        else
        {
            // Set startTime to next Thursday 00:00:00 UTC
            startTime = today.Date.AddDays(DayOfWeek.Thursday - today.DayOfWeek + 7);
            // Set startTime to next Thursday 00:00:00 UTC
            endTime = today.Date.AddDays(DayOfWeek.Thursday - today.DayOfWeek + 7).Add(new TimeSpan(23, 59, 59));
        }
        
        return  new StageTimeInDateTime
        {
            StartTime = new DateTime(2024, 7, 18, 0, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2024, 7, 18, 23, 59, 59, DateTimeKind.Utc)
        };
    }


    private async Task<List<ActivityRankData>> GetRankListAsync(string activityId)
    {
        StageTimeInDateTime stageTime;
        switch (activityId)
        {
            case ActivityConstants.SGR5RankActivityId:
                stageTime = GetInProgressStageTimeForSGR5();
                return await GetXPSGR5RankListAsync(stageTime.StartTime.ToUtcSeconds(), stageTime.EndTime.ToUtcSeconds());
            case ActivityConstants.SGR7RankActivityId:
                stageTime = GetInProgressStageTimeForSGR7();
                return await GetXPSGR7RankListAsync(stageTime.StartTime.ToUtcMilliSeconds(), stageTime.EndTime.ToUtcMilliSeconds());
            default:
                stageTime = GetInProgressStageTimeForSGR5();
                return await GetXPSGR5RankListAsync(stageTime.StartTime.ToUtcSeconds(), stageTime.EndTime.ToUtcSeconds());
        }
    }
    
    private async Task<List<ActivityRankData>> GetXPSGR5RankListAsync(long beginTime, long endTime)
    {
        var res = await _adoptGraphQlProvider.GetAdoptInfoByTime(beginTime, endTime);
        if (res.IsNullOrEmpty())
        {
            _logger.LogInformation("GetRank empty");
            return new List<ActivityRankData>();
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

            if (adopt.Gen - adopt.ParentGen > 1)
            {
                _logger.LogInformation("direct adoption to gen nine: {adoptId}", adopt.AdoptId);
                amount *= 9666;
            }
            else
            {
                amount *= 1314;
            }

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
                    Scores = kvp.Value.TotalAmount / (decimal)Math.Pow(10, 8),
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

        return rankDataList;
    }
    
    
    
    private async Task<List<ActivityRankData>> GetXPSGR7RankListAsync(long beginTime, long endTime)
    {
        var chainId = _levelOptions.CurrentValue.ChainIdForReal;
        
        var input = new GetSchrodingerSoldInput
        {
            TimestampMax = endTime,
            TimestampMin = beginTime,
            ChainId = chainId
        };
        var res = await _schrodingerCatProvider.GetSchrodingerSoldListAsync(input);
        
        var rankDataDict = new Dictionary<string, RankItem>();
        foreach (var item in res)
        {
            var address = item.To;
            if (address.IsNullOrEmpty() || address == "ELF_qYQLgEYVLyx3MatsUtW5sCbYooc5LQyuomFHvdmLbrESxMmeY_tDVV")
            {
                continue;
            }
            var amount = item.Amount * item.Price;
            var adoptTime = item.Timestamp;
            
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

        var year = DateTime.UtcNow.Year;
        var key = "ELFPrice:" + year + "-" + beginTime;
        var cache = await _distributedCache.GetAsync(key);
        decimal price;
        if (cache != null)
        {
            price = decimal.Parse(cache);
        }
        else
        {
            price = await GetELFPriceAsync(key);
        }
        
        
        var rankDataList = rankDataDict.Select(kvp => 
                new ActivityRankData
                {
                    Address = kvp.Key, 
                    Scores = kvp.Value.TotalAmount * 99 * price,
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

        return rankDataList;
    }

    private async Task<decimal> GetELFPriceAsync(string key)
    {
        var priceDto = await _awakenLiquidityProvider.GetPriceAsync("ELF", "USDT", "tDVV", "0.0005");
        var price = priceDto.Items.FirstOrDefault().Price;
        AssertHelper.IsTrue(price != null && price > 0, "ELF price is null or zero");

        await _distributedCache.SetAsync(key, price.ToString(), new DistributedCacheEntryOptions()
        {
            SlidingExpiration = TimeSpan.FromDays(7)
        });
        
        return price;
    }
    
    private ActivityRankOptions GetRankOptions(string activityId)
    {
        var activityOption = _activityOptions.CurrentValue.ActivityList.FirstOrDefault(a => a.ActivityId == activityId);
        return activityOption.RankOptions;
    }
}