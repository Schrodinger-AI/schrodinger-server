using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.Logging;
using Nest;
using SchrodingerServer.Common;
using SchrodingerServer.Zealy;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Background.Providers;

public interface IZealyProvider
{
    Task<List<ZealyUserIndex>> GetUsersAsync(int skipCount, int maxResultCount);
    Task<ZealyUserIndex> GetUserByIdAsync(string userId);
    Task<List<ZealyUserXpIndex>> GetUserXpsAsync(int skipCount, int maxResultCount);
    Task<ZealyUserXpIndex> GetUserXpByIdAsync(string id);
    Task<List<ZealyXpScoreIndex>> GetXpScoresAsync(int skipCount, int maxResultCount);

    Task<List<ZealyUserXpRecordIndex>> GetPendingUserXpsAsync(int skipCount, int maxResultCount, long startTime,
        long endTime);

    Task UserXpAddOrUpdateAsync(ZealyUserXpIndex zealyUserXp);
    Task XpRecordAddOrUpdateAsync(ZealyUserXpRecordIndex record);
}

public class ZealyProvider : IZealyProvider, ISingletonDependency
{
    private readonly ILogger<ZealyProvider> _logger;
    private readonly INESTRepository<ZealyUserIndex, string> _zealyUserRepository;
    private readonly INESTRepository<ZealyUserXpIndex, string> _zealyUserXpRepository;
    private readonly INESTRepository<ZealyXpScoreIndex, string> _zealyXpScoreRepository;
    private readonly INESTRepository<ZealyUserXpRecordIndex, string> _zealyXpRecordRepository;

    public ZealyProvider(INESTRepository<ZealyUserIndex, string> zealyUserRepository, ILogger<ZealyProvider> logger,
        INESTRepository<ZealyUserXpIndex, string> zealyUserXpRepository,
        INESTRepository<ZealyXpScoreIndex, string> zealyXpScoreRepository,
        INESTRepository<ZealyUserXpRecordIndex, string> zealyXpRecordRepository)
    {
        _zealyUserRepository = zealyUserRepository;
        _logger = logger;
        _zealyUserXpRepository = zealyUserXpRepository;
        _zealyXpScoreRepository = zealyXpScoreRepository;
        _zealyXpRecordRepository = zealyXpRecordRepository;
    }

    public async Task<List<ZealyUserIndex>> GetUsersAsync(int skipCount, int maxResultCount)
    {
        var (totalCount, data) =
            await _zealyUserRepository.GetListAsync(skip: skipCount, limit: maxResultCount);

        return data;
    }

    public async Task<ZealyUserIndex> GetUserByIdAsync(string userId)
    {
        if (userId.IsNullOrEmpty())
        {
            return null;
        }

        var mustQuery = new List<Func<QueryContainerDescriptor<ZealyUserIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.Id).Value(userId)));

        QueryContainer Filter(QueryContainerDescriptor<ZealyUserIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        return await _zealyUserRepository.GetAsync(Filter);
    }

    public async Task<List<ZealyUserXpIndex>> GetUserXpsAsync(int skipCount, int maxResultCount)
    {
        var (totalCount, data) =
            await _zealyUserXpRepository.GetListAsync(skip: skipCount, limit: maxResultCount);

        return data;
    }

    public async Task<ZealyUserXpIndex> GetUserXpByIdAsync(string id)
    {
        if (id.IsNullOrEmpty())
        {
            return null;
        }

        var mustQuery = new List<Func<QueryContainerDescriptor<ZealyUserXpIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.Id).Value(id)));

        QueryContainer Filter(QueryContainerDescriptor<ZealyUserXpIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        return await _zealyUserXpRepository.GetAsync(Filter);
    }

    public async Task UserXpAddOrUpdateAsync(ZealyUserXpIndex zealyUserXp)
    {
        await _zealyUserXpRepository.AddOrUpdateAsync(zealyUserXp);
    }

    public async Task XpRecordAddOrUpdateAsync(ZealyUserXpRecordIndex record)
    {
        await _zealyXpRecordRepository.AddOrUpdateAsync(record);
    }

    public async Task<List<ZealyXpScoreIndex>> GetXpScoresAsync(int skipCount, int maxResultCount)
    {
        var (totalCount, data) =
            await _zealyXpScoreRepository.GetListAsync(skip: skipCount, limit: maxResultCount);

        return data;
    }

    public async Task<List<ZealyUserXpRecordIndex>> GetPendingUserXpsAsync(int skipCount, int maxResultCount,
        long startTime, long endTime)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<ZealyUserXpRecordIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.Status).Value(ContractInvokeStatus.Pending.ToString())));

        if (startTime > 0)
        {
            mustQuery.Add(q => q.Range(i => i.Field(f => f.CreateTime).GreaterThanOrEquals(startTime)));
        }

        if (endTime > 0)
        {
            mustQuery.Add(q => q.Range(i => i.Field(f => f.CreateTime).LessThanOrEquals(endTime)));
        }

        QueryContainer Filter(QueryContainerDescriptor<ZealyUserXpRecordIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var (totalCount, data) =
            await _zealyXpRecordRepository.GetListAsync(Filter, skip: skipCount, limit: maxResultCount);

        return data;
    }
}