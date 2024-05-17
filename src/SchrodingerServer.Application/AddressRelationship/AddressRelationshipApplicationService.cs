using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SchrodingerServer.Common;
using SchrodingerServer.Options;
using SchrodingerServer.Points;
using SchrodingerServer.Points.Provider;
using SchrodingerServer.Users;
using SchrodingerServer.Users.Dto;
using SchrodingerServer.Users.Index;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace SchrodingerServer.AddressRelationship;

public class AddressRelationshipApplicationService : ApplicationService, IAddressRelationshipApplicationService
{
    private readonly IAddressRelationshipProvider _addressRelationshipProvider;
    private readonly ILogger<AddressRelationshipApplicationService> _logger;
    private readonly IPointSettleService _pointSettleService;
    private readonly IPointDailyRecordProvider _pointDailyRecordProvider;
    private readonly IOptionsMonitor<LevelOptions> _levelOptions;
    
    public AddressRelationshipApplicationService(
        IAddressRelationshipProvider addressRelationshipProvider,
        ILogger<AddressRelationshipApplicationService> logger, 
        IPointSettleService pointSettleService, 
        IPointDailyRecordProvider pointDailyRecordProvider, 
        IOptionsMonitor<LevelOptions> levelOptions)
    {
        _addressRelationshipProvider = addressRelationshipProvider;
        _logger = logger;
        _pointSettleService = pointSettleService;
        _pointDailyRecordProvider = pointDailyRecordProvider;
        _levelOptions = levelOptions;
    }
    
    public async Task BindAddressAsync(BindAddressInput input)
    {
        _logger.LogInformation("BindAddress input:{input}", JsonConvert.SerializeObject(input));
        var publicKeyVal = input.PublicKey;
        var signatureVal = input.Signature;
        var aelfAddress = input.AelfAddress;
        var evmAddress = input.EvmAddress;
        
        var publicKey = ByteArrayHelper.HexStringToByteArray(publicKeyVal);
        var signature = ByteArrayHelper.HexStringToByteArray(signatureVal);
        
        AssertHelper.IsTrue(CryptoHelper.RecoverPublicKey(signature,
            HashHelper.ComputeFrom(string.Join("-", aelfAddress, evmAddress)).ToByteArray(),
            out var managerPublicKey), "Invalid signature.");
        AssertHelper.IsTrue(managerPublicKey.ToHex() == publicKeyVal, "Invalid publicKey or signature.");
        
        var bindingExist = await _addressRelationshipProvider.CheckBindingExistsAsync(aelfAddress, evmAddress);
        if (bindingExist)
        {
            _logger.LogError("Binding already exists for aelfAddress: {aelfAddress} and evmAddress: {evmAddress}", aelfAddress, evmAddress);
            throw new UserFriendlyException("Binding already exists for address: " + aelfAddress + " and " + evmAddress);
        }
        
        await  _addressRelationshipProvider.BindAddressAsync(aelfAddress, evmAddress);
        await SettlePointsAsync(aelfAddress, evmAddress);

        _logger.LogInformation("BindAddress finished");
    }

    private async Task SettlePointsAsync(string aelfAddress, string evmAddress)
    {
        var chainId = _levelOptions.CurrentValue.ChainIdForReal;
        var pointName = "XPSGR-10";
        var bizDate = DateTime.UtcNow.ToString(TimeHelper.Pattern);
        var pointDailyRecordList = await _pointDailyRecordProvider.GetDailyRecordsByAddressAndPointNameAsync(evmAddress, pointName);
        _logger.LogInformation("SettlePoints record size:{size}", pointDailyRecordList.Count);

        if (pointDailyRecordList.IsNullOrEmpty())
        {
            _logger.LogInformation("{address} has no XGR-10 point", aelfAddress);
            return;
        }
        
        var batchList = SplitList(pointDailyRecordList, 20);
        _logger.LogInformation("SettlePoints batch size:{size}", batchList.Count);
        
        foreach (var tradeList in batchList)
        {
            var bizId = IdGenerateHelper.GetPointBizId(chainId, bizDate, pointName, Guid.NewGuid().ToString());
            _logger.LogInformation("SettlePoints process for bizId:{id}", bizId);

            try
            {
                var pointSettleDto = new PointSettleDto
                {
                    ChainId = chainId,
                    PointName = pointName,
                    BizId = bizId,
                    UserPointsInfos = tradeList.Select(item => new UserPointInfo
                    {
                        Id = item.Id,
                        Address = aelfAddress,
                        PointAmount = item.PointAmount
                    }).ToList()
                }; 
                
                var aggPointSettleDto = new PointSettleDto
                    {
                        ChainId = chainId,
                        PointName = pointName,
                        BizId = bizId,
                        UserPointsInfos = new List<UserPointInfo>()
                        {
                            new UserPointInfo
                            {
                                Id = tradeList.First().Id,
                                Address = aelfAddress,
                                PointAmount = tradeList.Sum(item => item.PointAmount)
                            }
                        }
                    }; 
                await _pointSettleService.BatchSettleAsync(aggPointSettleDto);
                
                await _pointDailyRecordProvider.UpdatePointDailyRecordAsync(pointSettleDto, PointRecordStatus.Success.ToString());
            }
            catch (Exception e)
            {
                _logger.LogError(e, "SettlePoints error, bizId:{bizId} ids:{ids}", bizId, 
                    string.Join(",", tradeList.Select(item => item.Id)));
            }
        }
    }
    
    
    private static List<List<PointDailyRecordIndex>> SplitList(List<PointDailyRecordIndex> records, int n)
    {
        return records
            .Select((item, index) => new { item, index })
            .GroupBy(x => x.index / n)
            .Select(g => g.Select(x => x.item).ToList())
            .ToList();
    }
}