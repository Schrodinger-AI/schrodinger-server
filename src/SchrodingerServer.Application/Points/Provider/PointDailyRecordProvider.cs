using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using GraphQL;
using Microsoft.Extensions.Logging;
using Nest;
using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.Common.GraphQL;
using SchrodingerServer.Dto;
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

    Task<List<PointDailyRecordIndex>> GetAllDailyRecordIndex(string chainId, string bizDate, string pointName);
    
    Task<List<PointDailyRecordIndex>> GetPendingDailyRecordIndex(string chainId);
    
    Task<List<PointsDetailDto>> GetPointsRecordByNameAsync(string pointsName);
}

public class PointDailyRecordProvider : IPointDailyRecordProvider, ISingletonDependency
{
    private readonly ILogger<PointDailyRecordProvider> _logger;
    private readonly INESTRepository<PointDailyRecordIndex, string> _pointDailyRecordIndexRepository;
    private readonly IClusterClient _clusterClient;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IObjectMapper _objectMapper;
    private readonly IGraphQLClientFactory _graphQlClientFactory;

    public PointDailyRecordProvider(INESTRepository<PointDailyRecordIndex, string> pointDailyRecordIndexRepository, 
        ILogger<PointDailyRecordProvider> logger, 
        IClusterClient clusterClient, 
        IGraphQLClientFactory graphQlClientFactory,
        IDistributedEventBus distributedEventBus, IObjectMapper objectMapper)
    {
        _pointDailyRecordIndexRepository = pointDailyRecordIndexRepository;
        _logger = logger;
        _clusterClient = clusterClient;
        _distributedEventBus = distributedEventBus;
        _objectMapper = objectMapper;
        _graphQlClientFactory = graphQlClientFactory;
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
    
    public async Task<List<PointDailyRecordIndex>> GetAllDailyRecordIndex(string chainId, string bizDate, string pointName)
    {
        var res = new List<PointDailyRecordIndex>();
        List<PointDailyRecordIndex> list;
        var skipCount = 0;
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

        do
        {
            list = (await _pointDailyRecordIndexRepository.GetSortListAsync(filterFunc: Filter, skip: skipCount, limit: 10000, sortFunc: sorting)).Item2;
            var count = list.Count;
            res.AddRange(list);
            if (list.IsNullOrEmpty() || count < 10000)
            {
                break;
            }
            skipCount += count;
        } while (!list.IsNullOrEmpty());

        return res;
    }
    
    
    public async Task<List<PointDailyRecordIndex>> GetPendingDailyRecordIndex(string chainId)
    {
        var res = new List<PointDailyRecordIndex>();
        List<PointDailyRecordIndex> list;
        var skipCount = 0;
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

        do
        {
            list = (await _pointDailyRecordIndexRepository.GetSortListAsync(filterFunc: Filter, skip: skipCount, limit: 10000, sortFunc: sorting)).Item2;
            var count = list.Count;
            res.AddRange(list);
            if (list.IsNullOrEmpty() || count < 10000)
            {
                break;
            }
            skipCount += count;
        } while (!list.IsNullOrEmpty());

        return res;
    }
    
    
    public async Task<List<PointsDetailDto>> GetPointsRecordByNameAsync(
        string pointsName)
    {
        var indexerResult = await _graphQlClientFactory.GetClient(GraphQLClientEnum.PointPlatform).SendQueryAsync<PointsDetailIndexerQueryDto>(new GraphQLRequest
        {
            Query =
                @"query($dappId:String!, $pointsName:String!, $address:String!){
                    getPointsRecordByName(input: {dappId:$dappId, pointsName:$pointsName, address:$address}){
                        totalRecordCount,
                        data{
                        id,
                        address,
                        domain,
                        role,
                        dappId,
    					pointsName,
    					actionName,
    					amount,
    					createTime,
    					updateTime
                    }
                }
            }",
            Variables = new
            {
                dappId = string.Empty,
                address = string.Empty,
                pointsName = pointsName
            }
        });

        return indexerResult.Data.GetPointsRecordByName.Data;
    }
}