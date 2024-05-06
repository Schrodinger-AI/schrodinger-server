using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Dto;
using SchrodingerServer.EntityEventHandler.Core.Options;
using SchrodingerServer.Points;
using SchrodingerServer.Points.Provider;
using SchrodingerServer.Users.Dto;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Threading;

namespace SchrodingerServer.EntityEventHandler.Core.Worker;

public class PointCompensateWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly ILogger<PointAccumulateForSGR11Worker> _logger;
    private readonly IOptionsMonitor<WorkerOptions> _workerOptionsMonitor;
    private readonly IPointDispatchProvider _pointDispatchProvider;

    private readonly IAbpDistributedLock _distributedLock;
    private readonly IPointSettleService _pointSettleService;
    private readonly IPointDailyRecordProvider _pointDailyRecordProvider;
    private readonly string _lockKey = "PointCompensateWorker";
    
    
    
    public PointCompensateWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<PointAccumulateForSGR11Worker> logger,
        IOptionsMonitor<WorkerOptions> workerOptionsMonitor,
        IPointDispatchProvider pointDispatchProvider,
        IAbpDistributedLock distributedLock,
        IPointSettleService pointSettleService,
        IPointDailyRecordProvider pointDailyRecordProvider): base(timer,
        serviceScopeFactory)
    {
        _logger = logger;
        _workerOptionsMonitor = workerOptionsMonitor;
        _pointDispatchProvider = pointDispatchProvider;
        _distributedLock = distributedLock;
        _pointSettleService = pointSettleService;
        _pointDailyRecordProvider = pointDailyRecordProvider;
        timer.Period = _workerOptionsMonitor.CurrentValue.GetWorkerPeriodMinutes(_lockKey) * 60 * 1000;
    }
    
    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        await using var handle = await _distributedLock.TryAcquireAsync(_lockKey);
        var openSwitch = _workerOptionsMonitor.CurrentValue.GetWorkerSwitch(_lockKey);
        _logger.LogInformation("PointCompensateWorker start openSwitch {openSwitch}", openSwitch);
        if (!openSwitch)
        {
            _logger.LogWarning("PointCompensateWorker has not open...");
            return;
        }
        
        var pointName = _workerOptionsMonitor.CurrentValue.GetWorkerPointName(_lockKey);
        var bizDate = DateTime.UtcNow.ToString(TimeHelper.Pattern);
        var isExecuted = await _pointDispatchProvider.GetDispatchAsync(PointDispatchConstants.SYNC_COMPENSATE_PREFIX , bizDate, pointName);
        if (isExecuted)
        {
            _logger.LogInformation("PointAccumulateForSGR9Worker has been executed for bizDate: {0} pointName:{1}", bizDate, pointName);
            return;
        }
        
       
        var chainIds = _workerOptionsMonitor.CurrentValue.ChainIds;
        foreach (var chainId in chainIds)
        {
            await CompensateAsync(pointName, chainId, bizDate);
        }
        _logger.LogInformation("PointCompensateWorker finished");
        
        await _pointDispatchProvider.SetDispatchAsync(PointDispatchConstants.SYNC_COMPENSATE_PREFIX, bizDate,
            pointName, true);
    }

    private async Task CompensateAsync(string pointName, string chainId, string bizDate)
    {
        _logger.LogInformation("PointCompensateWorker begin compensate pointName:{pointName}, chainId:{chainId}", pointName, chainId);

        var pointDetailList = await _pointDailyRecordProvider.GetPointsRecordByNameAsync(pointName);
        _logger.LogInformation("PointCompensateWorker compensate user size:{size}", pointDetailList.Count);

        var batchList = SplitList(pointDetailList, 20);
        _logger.LogInformation("PointCompensateWorker compensate batch size:{size}", batchList.Count);

        
        
        foreach (var tradeList in batchList)
        {
            var bizId = IdGenerateHelper.GetPointBizId(chainId, bizDate, pointName, Guid.NewGuid().ToString());
            _logger.LogInformation("PointCompensateWorker process for bizId:{id}", bizId);

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
                        Address = item.Address,
                        PointAmount = item.Amount * (100000000 - 1)
                    }).ToList()
                }; 
                await _pointSettleService.BatchSettleAsync(pointSettleDto);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "PointCompensateWorker Compensate error, bizId:{bizId} ids:{ids}", bizId, 
                    string.Join(",", tradeList.Select(item => item.Id)));
            }
        }
    }
    
    private static List<List<PointsDetailDto>> SplitList(List<PointsDetailDto> records, int n)
    {
        return records
            .Select((item, index) => new { item, index })
            .GroupBy(x => x.index / n)
            .Select(g => g.Select(x => x.item).ToList())
            .ToList();
    }
}
