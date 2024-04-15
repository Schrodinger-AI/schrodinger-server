using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.Logging;
using Nest;
using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.Grains.Grain.Points;
using SchrodingerServer.Users.Dto;
using SchrodingerServer.Users.Eto;
using SchrodingerServer.Users.Index;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Points.Provider;

public interface IPointDailyRecordProvider
{
    Task<List<PointDailyRecordIndex>> GetPointDailyRecordsAsync(string chainId, string bizDate, string pointName,
        int skipCount);

    Task<List<PointDailyRecordIndex>> GetPendingPointDailyRecordsAsync(string chainId, int skipCount);
    
    Task UpdatePointDailyRecordAsync(PointSettleDto settleDto,  string status);
}

public class PointDailyRecordProvider : IPointDailyRecordProvider, ISingletonDependency
{
    private readonly ILogger<PointDailyRecordProvider> _logger;
    private readonly INESTRepository<PointDailyRecordIndex, string> _pointDailyRecordIndexRepository;
    private readonly IClusterClient _clusterClient;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IObjectMapper _objectMapper;

    public PointDailyRecordProvider(INESTRepository<PointDailyRecordIndex, string> pointDailyRecordIndexRepository, 
        ILogger<PointDailyRecordProvider> logger, IClusterClient clusterClient, 
        IDistributedEventBus distributedEventBus, IObjectMapper objectMapper)
    {
        _pointDailyRecordIndexRepository = pointDailyRecordIndexRepository;
        _logger = logger;
        _clusterClient = clusterClient;
        _distributedEventBus = distributedEventBus;
        _objectMapper = objectMapper;
    }

    public async Task<List<PointDailyRecordIndex>> GetPointDailyRecordsAsync(string chainId, string bizDate, string pointName,
        int skipCount)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<PointDailyRecordIndex>, QueryContainer>>();
        
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.ChainId).Value(chainId)));
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.BizDate).Value(bizDate)));
        mustQuery.Add(q => q.Range(i =>
            i.Field(f => f.PointAmount).GreaterThan(0)));
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.PointName).Value(pointName)));
        mustQuery.Add(q => 
            !q.Exists(e => e.Field(f => f.BizId)));
        
        QueryContainer Filter(QueryContainerDescriptor<PointDailyRecordIndex> f) =>
            f.Bool(b => b.Must(mustQuery));
        
        var sorting = new Func<SortDescriptor<PointDailyRecordIndex>, IPromise<IList<ISort>>>(s =>
            s.Ascending(t => t.CreateTime));
        
        var tuple = await _pointDailyRecordIndexRepository.GetSortListAsync(Filter, skip: skipCount, sortFunc: sorting);
        return tuple.Item2;
    }
    
    public async Task<List<PointDailyRecordIndex>> GetPendingPointDailyRecordsAsync(string chainId, int skipCount)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<PointDailyRecordIndex>, QueryContainer>>();
        
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.ChainId).Value(chainId)));
        
        mustQuery.Add(q => q.Range(i =>
            i.Field(f => f.PointAmount).GreaterThan(0)));
        
        mustQuery.Add(q => 
            q.Exists(e => e.Field(f => f.BizId)));
        
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.Status).Value(PointRecordStatus.Pending.ToString())));
        
        QueryContainer Filter(QueryContainerDescriptor<PointDailyRecordIndex> f) =>
            f.Bool(b => b.Must(mustQuery));
        
        var sorting = new Func<SortDescriptor<PointDailyRecordIndex>, IPromise<IList<ISort>>>(s =>
            s.Ascending(t => t.CreateTime));
        
        var tuple = await _pointDailyRecordIndexRepository.GetSortListAsync(Filter, skip: skipCount, sortFunc: sorting);
        return tuple.Item2;
    }

    public async Task UpdatePointDailyRecordAsync(PointSettleDto settleDto, string status)
    {
        var bizId = settleDto.BizId;
        var userPointInfos = settleDto.UserPointsInfos;
        _logger.LogInformation("UpdatePointDailyRecord bizId:{bizId} status:{status}", bizId, status);
        foreach (var userPoints in userPointInfos)
        {
            try
            {
                var pointDailyRecordGrain = _clusterClient.GetGrain<IPointDailyRecordGrain>(userPoints.Id);
                var result = await pointDailyRecordGrain.UpdateAsync(bizId, status);
                if (!result.Success)
                {
                    _logger.LogError("PointDailyRecordGrain UpdateAsync fail, id: {id}.", userPoints.Id);
                }
                await _distributedEventBus.PublishAsync(
                    _objectMapper.Map<PointDailyRecordGrainDto, PointDailyRecordEto>(result.Data));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "PointDailyRecordGrain UpdateAsync fail, id: {id}.", userPoints.Id);
            }
        }
    }
}