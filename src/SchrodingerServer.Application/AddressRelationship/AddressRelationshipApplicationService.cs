using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using Microsoft.Extensions.Logging;
using SchrodingerServer.Adopts;
using SchrodingerServer.Common;
using SchrodingerServer.Dto;
using SchrodingerServer.Points;
using SchrodingerServer.Points.Provider;
using SchrodingerServer.Users;
using SchrodingerServer.Users.Dto;
using SchrodingerServer.Users.Index;
using Volo.Abp.Application.Services;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.AddressRelationship;

public class AddressRelationshipApplicationService : ApplicationService, IAddressRelationshipApplicationService
{
    private readonly IAddressRelationshipProvider _addressRelationshipProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<AddressRelationshipApplicationService> _logger;
    private readonly IPointSettleService _pointSettleService;
    private readonly IPointDailyRecordProvider _pointDailyRecordProvider;
    
    public AddressRelationshipApplicationService(
        IAddressRelationshipProvider addressRelationshipProvider,
        IObjectMapper objectMapper,
        ILogger<AddressRelationshipApplicationService> logger, 
        IPointSettleService pointSettleService, 
        IPointDailyRecordProvider pointDailyRecordProvider)
    {
        _addressRelationshipProvider = addressRelationshipProvider;
        _objectMapper = objectMapper;
        _logger = logger;
        _pointSettleService = pointSettleService;
        _pointDailyRecordProvider = pointDailyRecordProvider;
    }
    
    public async Task BindAddressAsync(BindAddressInput input)
    {
        var publicKeyVal = input.PublicKey;
        var signatureVal = input.Signature;
        // var timestamp = input.Timestamp.ToString();
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
            return;
        }
        
        await  _addressRelationshipProvider.BindAddressAsync(aelfAddress, evmAddress);
    }

    private async Task SettlePointsAsync(string aelfAddress, string evmAddress)
    {
        var pointName = "XPSGR-10";
        var chainId = "tDVV";
        var bizDate = DateTime.UtcNow.ToString(TimeHelper.Pattern);
        var pointDailyRecordList = await _pointDailyRecordProvider.GetDailyRecordsByAddressAndPointNameAsync(aelfAddress, pointName);
        _logger.LogInformation("PointCompensateWorker compensate user size:{size}", pointDailyRecordList.Count);
        
        var bizId = IdGenerateHelper.GetPointBizId(chainId, bizDate, pointName, Guid.NewGuid().ToString());
        _logger.LogInformation("PointCompensateWorker process for bizId:{id}", bizId);
        
        
        
        
        // var totalPointAmount = pointDailyRecordList.Sum(x => x.PointAmount);
        // var userInfos = new List<UserPointInfo>
        // {
        //     new UserPointInfo
        //     {
        //         Id = bizId,
        //         Address = evmAddress,
        //         PointAmount = totalPointAmount
        //     }
        // };
        //
        // try
        // {
        //     var pointSettleDto = new PointSettleDto
        //     {
        //         ChainId = chainId,
        //         PointName = pointName,
        //         BizId = bizId,
        //         UserPointsInfos = userInfos
        //     }; 
        //     await _pointSettleService.BatchSettleAsync(pointSettleDto);
        // }
        // catch (Exception e)
        // {
        //     _logger.LogError(e, "SettlePointsAsync error, bizId:{bizId}, evmAddress:{evmAddress}, " +
        //                         "aelfAddress:{aelfAddress}", bizId, evmAddress, aelfAddress);
        // }
        
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