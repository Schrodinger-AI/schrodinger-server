using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NUglify.Helpers;
using SchrodingerServer.AddressRelationship.Dto;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Users;
using SchrodingerServer.Users.Index;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Activity;

public class ActivityApplicationService : ApplicationService, IActivityApplicationService
{
    private readonly IAddressRelationshipProvider _addressRelationshipProvider;
    private readonly ILogger<ActivityApplicationService> _logger;
    private IOptionsMonitor<ActivityOptions> _activityOptions;
    private readonly IObjectMapper _objectMapper;
    
    public ActivityApplicationService(IAddressRelationshipProvider addressRelationshipProvider, 
        ILogger<ActivityApplicationService> logger, IOptionsMonitor<ActivityOptions> activityOptions, 
        IObjectMapper objectMapper)
    {
        _addressRelationshipProvider = addressRelationshipProvider;
        _logger = logger;
        _activityOptions = activityOptions;
        _objectMapper = objectMapper;
    }

    public async Task<List<ActivityDto>> GetActivityListAsync(GetActivityListInput input)
    {
        var activityOptions = _activityOptions.CurrentValue;

        var activityList = activityOptions.ActivityList.Where(a => a.IsShow).ToList();
        var activityDtoList = _objectMapper.Map<List<ActivityInfo>, List<ActivityDto>>(activityList).Skip(input.SkipCount).Take(input.MaxResultCount);
        
        activityDtoList.ForEach(activity =>
        {
            var beginTime = DateTimeOffset.FromUnixTimeSeconds(activity.BeginTime).UtcDateTime;
            var timeDiff = DateTime.UtcNow - beginTime;
            if (timeDiff.TotalSeconds < activityOptions.NewTagInterval)
            {
                activity.IsNew = true;
            }
        });

        return activityDtoList.ToList();
    }

    public async Task<ActivityInfoDto> GetActivityInfoAsync()
    {
        var activityOptions = _activityOptions.CurrentValue;

        var hasNewActivity = activityOptions.ActivityList.Any(activity =>
        {
            var beginTime = DateTimeOffset.FromUnixTimeSeconds(activity.BeginTime).UtcDateTime;
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
}