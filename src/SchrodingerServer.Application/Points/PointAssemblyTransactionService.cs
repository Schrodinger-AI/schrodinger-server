using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.Grains.Grain.Points;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Points.Provider;
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
    
    public PointAssemblyTransactionService(IPointSettleService pointSettleService,
        ILogger<PointAssemblyTransactionService> logger, IPointDailyRecordProvider pointDailyRecordProvider, 
        IOptionsMonitor<PointTradeOptions> pointTradeOptions, IClusterClient clusterClient)
    {
        _pointSettleService = pointSettleService;
        _logger = logger;
        _pointDailyRecordProvider = pointDailyRecordProvider;
        _pointTradeOptions = pointTradeOptions;
        _clusterClient = clusterClient;
    }

    public async Task AssembleAsync(string chainId, string bizDate, string pointName)
    {
        var skipCount = 0;
        List<PointDailyRecordIndex> pointDailyRecords;
        do
        {
            pointDailyRecords = await _pointDailyRecordProvider.GetPointDailyRecordsAsync(chainId, bizDate, pointName, skipCount);
            _logger.LogInformation(
                "GetPointDailyRecords chainId:{chainId} bizDate: {bizDate} pointName: {pointName} skipCount: {skipCount} count: {count}",
                chainId, bizDate, pointName, skipCount, pointDailyRecords?.Count);
            if (pointDailyRecords.IsNullOrEmpty())
            {
                break;
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

            skipCount += pointDailyRecords.Count;
        } while (!pointDailyRecords.IsNullOrEmpty());
    }

    public async Task SendAsync(string chainId)
    {
        var skipCount = 0;
        List<PointDailyRecordIndex> pointDailyRecords;
        do
        {
            pointDailyRecords = await _pointDailyRecordProvider.GetPendingPointDailyRecordsAsync(chainId, skipCount);
            _logger.LogInformation(
                "GetPendingPointDailyRecordsAsync chainId:{chainId} skipCount: {skipCount} count: {count}",
                chainId, skipCount, pointDailyRecords?.Count);
            if (pointDailyRecords.IsNullOrEmpty())
            {
                break;
            }

            var bizIds = pointDailyRecords.Select(record => record.BizId).ToHashSet();

            foreach (var bizId in bizIds)
            {
                await HandleSendPointRecord(bizId);
            }

            skipCount += pointDailyRecords.Count;
        } while (!pointDailyRecords.IsNullOrEmpty());
    }

    private async Task HandlePointRecords(string chainId, string bizDate, string pointName, List<PointDailyRecordIndex> records)
    {
        var batchList = SplitList(records, _pointTradeOptions.CurrentValue.MaxBatchSize);

        foreach (var tradeList in batchList)
        {
            var bizId = IdGenerateHelper.GetPointBizId(chainId, bizDate, pointName, Guid.NewGuid().ToString());
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
    }
    
    private async Task HandleSendPointRecord(string bizId)
    {
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
        }
        catch (Exception e)
        {
            _logger.LogError(e, "HandleSendPointRecords error, bizId: {bizId}.", bizId);
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