using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Newtonsoft.Json;
using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.Grains.Grain.Points;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Points.Provider;
using SchrodingerServer.Users;
using SchrodingerServer.Users.Dto;
using SchrodingerServer.Users.Index;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Points;

public interface IPointAssemblyTransactionService
{
    Task AssembleAsync(string chainId, string bizDate, string pointName);
    
    Task SendAsync(string chainId);
}

public class PointAssemblyTransactionService : IPointAssemblyTransactionService, ISingletonDependency
{
    private readonly IOptionsMonitor<PointTradeOptions> _pointTradeOptions;
    private readonly ILogger<PointAssemblyTransactionService> _logger;
    private readonly IPointSettleService _pointSettleService;
    private readonly IPointDailyRecordProvider _pointDailyRecordProvider;
    private readonly IClusterClient _clusterClient;
    private readonly IAddressRelationshipProvider _addressRelationshipProvider;
    
    public PointAssemblyTransactionService(IPointSettleService pointSettleService,
        ILogger<PointAssemblyTransactionService> logger, IPointDailyRecordProvider pointDailyRecordProvider, 
        IOptionsMonitor<PointTradeOptions> pointTradeOptions, IClusterClient clusterClient, 
        IAddressRelationshipProvider addressRelationshipProvider)
    {
        _pointSettleService = pointSettleService;
        _logger = logger;
        _pointDailyRecordProvider = pointDailyRecordProvider;
        _pointTradeOptions = pointTradeOptions;
        _clusterClient = clusterClient;
        _addressRelationshipProvider = addressRelationshipProvider;
    }

    public async Task AssembleAsync(string chainId, string bizDate, string pointName)
    {
        var pointDailyRecords = await _pointDailyRecordProvider.GetAllDailyRecordIndex(chainId, bizDate, pointName);
        _logger.LogInformation(
            "GetPointDailyRecords chainId:{chainId} bizDate: {bizDate} pointName: {pointName} count: {count}",
            chainId, bizDate, pointName, pointDailyRecords?.Count);
       
        if (pointDailyRecords.IsNullOrEmpty())
        {
            return;
        }
        
        if (pointName.EndsWith("-10"))
        {
            var recordsWithAddressBound = new  List<PointDailyRecordIndex>();
            foreach (var record in pointDailyRecords)
            {
                var evmAddress = record.Address;
                
                var aelfAddress = await _addressRelationshipProvider.GetAelfAddressByEvmAddressAsync(evmAddress);
                if (!aelfAddress.IsNullOrEmpty())
                {
                    record.Address = aelfAddress;
                    recordsWithAddressBound.Add(record);
                    _logger.LogInformation("Binding address found for record: {record}", JsonConvert.SerializeObject(record));
                }
            }

            if (recordsWithAddressBound.IsNullOrEmpty())
            {
                _logger.LogInformation("record with binding address is empty");
                return;
            }
            
            pointDailyRecords = recordsWithAddressBound;
        }

        var assemblyDict = pointDailyRecords.GroupBy(balance => balance.PointName)
            .ToDictionary(
                group => group.Key,
                group => group.ToList()
            );
            
        foreach (var (txPointName, records) in assemblyDict)
        {
            //Every pointName，Split batches to send transactions
            await HandlePointRecords(chainId, bizDate, txPointName, records);
        }
    }
    
    public async Task SendAsync(string chainId)
    {
        var pointDailyRecords = await _pointDailyRecordProvider.GetPendingDailyRecordIndex(chainId);
        _logger.LogInformation(
            "GetPendingPointDailyRecordsAsync chainId:{chainId}  count: {count}", chainId, pointDailyRecords?.Count);
        if (pointDailyRecords.IsNullOrEmpty())
        {
            return;
        }

        var bizIds = pointDailyRecords.Select(record => record.BizId).ToHashSet();

        foreach (var bizId in bizIds)
        {
            await HandleSendPointRecord(bizId);
        }
    }

    private async Task HandlePointRecords(string chainId, string bizDate, string pointName, List<PointDailyRecordIndex> records)
    {
        _logger.LogInformation(
            "HandlePointRecords begin chainId:{chainId}  count: {count}", chainId,  records.Count);
        var batchList = SplitList(records, _pointTradeOptions.CurrentValue.MaxBatchSize);
        _logger.LogInformation("SplitList success count: {count}",  batchList.Count);

        foreach (var tradeList in batchList)
        {
            var bizId = IdGenerateHelper.GetPointBizId(chainId, bizDate, pointName, Guid.NewGuid().ToString());
            _logger.LogInformation(
                "Prepare to Assemble chainId:{chainId} count: {count} bizId: {bidId} tradeList:{tradeList}", chainId,  tradeList.Count, bizId, JsonConvert.SerializeObject(tradeList));
            try
            {
                var pointSettleDto = PointSettleDto.Of(chainId, pointName, bizId, tradeList);
                var pointAssemblyTransactionGrain = _clusterClient.GetGrain<IPointAssemblyTransactionGrain>(bizId);
                var pointAssemblyGrainResult = await pointAssemblyTransactionGrain.CreateAsync(
                        PointAssemblyTransactionGrainDto.Of(bizId, pointSettleDto));
                if (!pointAssemblyGrainResult.Success)
                {
                    _logger.LogWarning("PointAssemblyTransactionGrain Create failed {bizId}", bizId);
                    return;
                }
                //Update PointDailyRecord Pending, if Update Grain Fail or Final Update Es failed, repackage to generate bizId
                await _pointDailyRecordProvider.UpdatePointDailyRecordAsync(pointSettleDto, PointRecordStatus.Pending.ToString());
            }
            catch (Exception e)
            {
                _logger.LogError(e, "BatchSettle BulkAddOrUpdateAsync error, bizId:{bizId} ids:{ids}", bizId, 
                    string.Join(",", tradeList.Select(item => item.Id)));
            }
        }
        
        _logger.LogInformation("HandlePointRecords finish");
    }
    
    private async Task HandleSendPointRecord(string bizId)
    {
        _logger.LogInformation(
            "HandleSendPointRecord begin bizId:{bizId}", bizId);
        try
        {
            var pointAssemblyTransactionGrain = _clusterClient.GetGrain<IPointAssemblyTransactionGrain>(bizId);
            var pointAssemblyGrainResult = await pointAssemblyTransactionGrain.GetAsync();
            if (!pointAssemblyGrainResult.Success)
            {
                _logger.LogWarning("PointAssemblyTransactionGrain Get failed {bizId}", bizId);
                return;
            }
            var settleDto = pointAssemblyGrainResult.Data.PointSettleDto;
            //Send Batch Settle
            await _pointSettleService.BatchSettleAsync(settleDto);
            
            await _pointDailyRecordProvider.UpdatePointDailyRecordAsync(settleDto, PointRecordStatus.Success.ToString());
            
            _logger.LogInformation(
                "HandleSendPointRecord finish bizId:{bizId}", bizId);
        }
        catch (Exception e)
        {
            _logger.LogError( "HandleSendPointRecords error, bizId: {bizId}, err: {}", bizId, e.Message);
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