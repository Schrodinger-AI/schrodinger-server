using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SchrodingerServer.AddressRelationship.Dto;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
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
    private readonly IOptionsMonitor<PointServiceOptions> _pointServiceOptions;
    
    public AddressRelationshipApplicationService(
        IAddressRelationshipProvider addressRelationshipProvider,
        ILogger<AddressRelationshipApplicationService> logger, 
        IPointSettleService pointSettleService, 
        IPointDailyRecordProvider pointDailyRecordProvider, 
        IOptionsMonitor<LevelOptions> levelOptions, 
        IOptionsMonitor<PointServiceOptions> pointServiceOptions)
    {
        _addressRelationshipProvider = addressRelationshipProvider;
        _logger = logger;
        _pointSettleService = pointSettleService;
        _pointDailyRecordProvider = pointDailyRecordProvider;
        _levelOptions = levelOptions;
        _pointServiceOptions = pointServiceOptions;
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


        var evmAddressToLower = evmAddress.ToLower();
        var bindingExist = await _addressRelationshipProvider.CheckBindingExistsAsync(aelfAddress, evmAddressToLower);
        if (bindingExist)
        {
            _logger.LogError("Binding already exists for aelfAddress: {aelfAddress} and evmAddress: {evmAddress}", aelfAddress, evmAddressToLower);
            throw new UserFriendlyException("This EVM address has been bound to an aelf address and cannot be bound again");
        }
        
        await  _addressRelationshipProvider.BindAddressAsync(aelfAddress, evmAddressToLower);
        await SettlePointsAsync(aelfAddress, evmAddressToLower);

        _logger.LogInformation("BindAddress finished");
    }

    private async Task SettlePointsAsync(string aelfAddress, string evmAddress)
    {
        var chainId = _levelOptions.CurrentValue.ChainIdForReal;
        var pointName = "XPSGR-10";
        var bizDate = DateTime.UtcNow.ToString(TimeHelper.Pattern);
        _logger.LogInformation("check point records for  address:{address}, point:{point}", evmAddress, pointName);
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
    }
    
    
    private static List<List<PointDailyRecordIndex>> SplitList(List<PointDailyRecordIndex> records, int n)
    {
        return records
            .Select((item, index) => new { item, index })
            .GroupBy(x => x.index / n)
            .Select(g => g.Select(x => x.item).ToList())
            .ToList();
    }


    public async Task<RemainPointDto> GetRemainPointAsync()
    {
        var address = _pointServiceOptions.CurrentValue.Address;
        var pointDailyRecordList = await _pointDailyRecordProvider.GetDailyRecordsByAddressAndPointNameAsync(address, "XPSGR-10");
        
        decimal totalPoints = pointDailyRecordList.Sum(item => item.PointAmount);

        var data = new List<UnboundEvmAddressPoints>
        {
            new UnboundEvmAddressPoints
            {
                Address = address,
                Points = totalPoints.ToString(CultureInfo.CurrentCulture)
            }
        };
        return new RemainPointDto
        {
            RemainPointList = data
        };
    }
}