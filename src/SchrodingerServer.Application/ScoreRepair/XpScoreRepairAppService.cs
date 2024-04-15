using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.Logging;
using Nest;
using Newtonsoft.Json;
using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.ContractInvoke.Index;
using SchrodingerServer.Grains.Grain.ZealyScore;
using SchrodingerServer.Grains.Grain.ZealyScore.Dtos;
using SchrodingerServer.Points;
using SchrodingerServer.ScoreRepair.Dtos;
using SchrodingerServer.Users.Dto;
using SchrodingerServer.Zealy;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.ScoreRepair;

public class XpScoreRepairAppService : IXpScoreRepairAppService, ISingletonDependency
{
    private readonly ILogger<XpScoreRepairAppService> _logger;
    private readonly INESTRepository<ZealyXpScoreIndex, string> _zealyXpScoreRepository;
    private readonly IObjectMapper _objectMapper;
    private readonly IClusterClient _clusterClient;
    private readonly INESTRepository<ZealyUserIndex, string> _zealyUserRepository;
    private readonly INESTRepository<ZealyUserXpRecordIndex, string> _zealyXpRecordRepository;
    private readonly INESTRepository<ContractInvokeIndex, string> _contractInvokeIndexRepository;
    private readonly IPointSettleService _pointSettleService;

    public XpScoreRepairAppService(ILogger<XpScoreRepairAppService> logger,
        INESTRepository<ZealyXpScoreIndex, string> zealyXpScoreRepository, IObjectMapper objectMapper,
        IClusterClient clusterClient, INESTRepository<ZealyUserIndex, string> zealyUserRepository,
        INESTRepository<ZealyUserXpRecordIndex, string> zealyXpRecordRepository,
        INESTRepository<ContractInvokeIndex, string> contractInvokeIndexRepository,
        IPointSettleService pointSettleService)
    {
        _logger = logger;
        _zealyXpScoreRepository = zealyXpScoreRepository;
        _objectMapper = objectMapper;
        _clusterClient = clusterClient;
        _zealyUserRepository = zealyUserRepository;
        _zealyXpRecordRepository = zealyXpRecordRepository;
        _contractInvokeIndexRepository = contractInvokeIndexRepository;
        _pointSettleService = pointSettleService;
    }

    public async Task UpdateScoreRepairDataAsync(List<UpdateXpScoreRepairDataDto> input)
    {
        _logger.LogInformation("begin to update score, data:{data}", JsonConvert.SerializeObject(input));

        var userIds = input.Select(t => t.UserId).ToList();
        var scores = await GetXpDataAsync(userIds);
        var scoreInfos = _objectMapper.Map<List<UpdateXpScoreRepairDataDto>, List<ZealyXpScoreIndex>>(input);

        var timeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        foreach (var score in scores)
        {
            var newScore = scoreInfos.FirstOrDefault(t => t.Id == score.Id);
            if (newScore == null) continue;

            score.LastRawScore = score.RawScore;
            score.LastActualScore = score.ActualScore;
            score.RawScore = newScore.RawScore;
            score.ActualScore = newScore.ActualScore;

            score.UpdateTime = timeSeconds;
        }

        if (!scores.IsNullOrEmpty())
        {
            await _zealyXpScoreRepository.BulkAddOrUpdateAsync(scores);
        }

        var scoreIds = scores.Select(t => t.Id).ToList();
        scoreInfos.RemoveAll(t => scoreIds.Contains(t.Id));

        foreach (var scoreInfo in scoreInfos)
        {
            scoreInfo.CreateTime = timeSeconds;
            scoreInfo.UpdateTime = timeSeconds;
        }

        if (!scoreInfos.IsNullOrEmpty())
        {
            await _zealyXpScoreRepository.BulkAddOrUpdateAsync(scoreInfos);
        }
    }

    public async Task<XpScoreRepairDataPageDto> GetXpScoreRepairDataAsync(XpScoreRepairDataRequestDto input)
    {
        var scoreInfos = await GetXpDataAsync(input);

        return new XpScoreRepairDataPageDto
        {
            TotalCount = scoreInfos.totalCount,
            Data = _objectMapper.Map<List<ZealyXpScoreIndex>, List<XpScoreRepairDataDto>>(scoreInfos.data)
        };
    }

    public async Task<UserXpInfoDto> GetUserXpAsync(UserXpInfoRequestDto input)
    {
        if (input.Address.IsNullOrEmpty() && input.UserId.IsNullOrEmpty())
        {
            return null;
        }

        if (!input.UserId.IsNullOrEmpty() && input.UserId.Length > 5)
        {
            return await GetUserXpByIdAsync(input.UserId);
        }

        var userInfo = await GetUserXpByAddressAsync(input.Address);
        if (userInfo == null)
        {
            throw new UserFriendlyException("user not exist.");
        }

        return await GetUserXpByIdAsync(userInfo.Id);
    }

    public async Task<XpRecordPageResultDto> GetUserRecordsAsync(string userId, int skipCount, int maxResultCount)
    {
        var result = new XpRecordPageResultDto();
        var records = await GetRecordsAsync(userId, skipCount, maxResultCount);
        result.Data = _objectMapper.Map<List<ZealyUserXpRecordIndex>, List<XpRecordDto>>(records.data);
        result.TotalCount = records.totalCount;

        return result;
    }

    public async Task ReCreateContractAsync(ReCreateDto input)
    {
        var contact = await GetContractInvokeTxByIdAsync(input.BizId);
        if (contact != null)
        {
            throw new UserFriendlyException("contract exist.");
        }

        var records = await GetRecordsByBizIdAsync(input.BizId);
        if (records.totalCount > 20)
        {
            throw new UserFriendlyException("record count more than 20.");
        }

        if (records.totalCount <= 0)
        {
            throw new UserFriendlyException("pending record not exist.");
        }

        await BatchSettleAsync(input.BizId, records.data);
    }

    private async Task<(List<ZealyXpScoreIndex> data, long totalCount)> GetXpDataAsync(
        XpScoreRepairDataRequestDto input)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<ZealyXpScoreIndex>, QueryContainer>>();

        if (!input.UserId.IsNullOrEmpty())
        {
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.Id).Value(input.UserId)));
        }

        QueryContainer Filter(QueryContainerDescriptor<ZealyXpScoreIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var (totalCount, data) = await _zealyXpScoreRepository.GetListAsync(Filter, skip: input.SkipCount,
            limit: input.MaxResultCount);

        return (data, totalCount);
    }

    private async Task<List<ZealyXpScoreIndex>> GetXpDataAsync(List<string> userIds)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<ZealyXpScoreIndex>, QueryContainer>>();
        if (userIds.IsNullOrEmpty())
        {
            return new List<ZealyXpScoreIndex>();
        }

        mustQuery.Add(q => q.Terms(i =>
            i.Field(f => f.Id).Terms(userIds)));

        QueryContainer Filter(QueryContainerDescriptor<ZealyXpScoreIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var (totalCount, data) = await _zealyXpScoreRepository.GetListAsync(Filter);
        return data;
    }

    public async Task<ZealyUserIndex> GetUserXpByAddressAsync(string address)
    {
        if (address.IsNullOrEmpty())
        {
            return null;
        }

        var mustQuery = new List<Func<QueryContainerDescriptor<ZealyUserIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.Address).Value(address)));

        QueryContainer Filter(QueryContainerDescriptor<ZealyUserIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        return await _zealyUserRepository.GetAsync(Filter);
    }

    private async Task<(long totalCount, List<ZealyUserXpRecordIndex> data)> GetRecordsAsync(string userId,
        int skipCount, int maxResultCount)
    {
        if (userId.IsNullOrEmpty())
        {
            throw new UserFriendlyException("userId can not be null");
        }

        var mustQuery = new List<Func<QueryContainerDescriptor<ZealyUserXpRecordIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.UserId).Value(userId)));

        QueryContainer Filter(QueryContainerDescriptor<ZealyUserXpRecordIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var (totalCount, data) =
            await _zealyXpRecordRepository.GetListAsync(Filter, skip: skipCount, limit: maxResultCount);

        return (totalCount, data);
    }

    private async Task<UserXpInfoDto> GetUserXpByIdAsync(string userId)
    {
        var userXpGrain = _clusterClient.GetGrain<IZealyUserXpGrain>(userId);
        var result = await userXpGrain.GetUserXpInfoAsync();
        if (!result.Success)
        {
            throw new UserFriendlyException($"get user xp info fail, message:{result.Message}");
        }

        return _objectMapper.Map<ZealyUserXpGrainDto, UserXpInfoDto>(result.Data);
    }

    private async Task<ContractInvokeIndex> GetContractInvokeTxByIdAsync(string bizId)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<ContractInvokeIndex>, QueryContainer>>
        {
            q => q.Term(i => i.Field(f => f.BizId).Value(bizId))
        };

        QueryContainer Filter(QueryContainerDescriptor<ContractInvokeIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        return await _contractInvokeIndexRepository.GetAsync(Filter);
    }

    private async Task<(long totalCount, List<ZealyUserXpRecordIndex> data)> GetRecordsByBizIdAsync(string bizId)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<ZealyUserXpRecordIndex>, QueryContainer>>();

        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.Status).Value(ContractInvokeStatus.Pending.ToString())));

        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.BizId).Value(bizId)));

        QueryContainer Filter(QueryContainerDescriptor<ZealyUserXpRecordIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var (totalCount, data) =
            await _zealyXpRecordRepository.GetListAsync(Filter);

        return (totalCount, data);
    }

    private async Task BatchSettleAsync(string bizId, List<ZealyUserXpRecordIndex> records)
    {
        var pointSettleDto = new PointSettleDto()
        {
            ChainId = CommonConstant.TDVVChainId,
            BizId = bizId,
            PointName = CommonConstant.ZealyPointName
        };

        var points = records.Select(record => new UserPointInfo()
            { Address = record.Address, PointAmount = record.PointsAmount }).ToList();

        pointSettleDto.UserPointsInfos = points;
        await _pointSettleService.BatchSettleAsync(pointSettleDto);
        _logger.LogInformation("BatchSettle finish, bizId:{bizId}", bizId);
    }
}