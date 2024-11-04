using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using GraphQL;
using GraphQL.Validation.Rules;
using Microsoft.Extensions.Logging;
using Nest;
using SchrodingerServer.Common;
using SchrodingerServer.Common.GraphQL;
using SchrodingerServer.Tasks.Dtos;
using SchrodingerServer.Users.Index;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Tasks.Provider;

public interface ITasksProvider
{
    Task<List<TasksDto>> GetTasksAsync(GetTasksInput input);
    Task<TasksDto> ChangeTaskStatusAsync(ChangeTaskStatusInput input);

    Task AddTasksAsync(List<TasksDto> tasks);

    // Task<decimal> UpdateUserTaskScoreAsync(string address, decimal score);
    Task AddTaskScoreDetailAsync(AddTaskScoreDetailInput input);

    // Task<decimal> GetScoreAsync(string address);
    Task<List<UserReferralDto>> GetInviteRecordsToday(List<string> addressList);

    // Task<List<TaskScoreDetailDto>> GetScoreDetailByAddressAsync(string address);
    Task<List<SpinDto>> GetUnfinishedSpinAsync(string address);
    Task<decimal> GetTotalScoreFromTask(string address);
    Task<decimal> GetConsumeScoreFromSpin(string address);
    Task<decimal> GetScoreFromSpinRewardAsync(string address);
    Task<bool> IsSpinFinished(string seed);
    Task FinishSpinAsync(string seed);
    Task AddSpinAsync(AddSpinInput input);
    Task<VoucherAdoptionDto> GetVoucherAdoptionAsync(string voucherId);
    Task<SpinRewardConfigDto> GetSpinRewardConfigAsync();
    Task<int> GetFinishedSpinCountAsync(string address);
    Task AddTgBotLogAsync(LogTgBotInput record);
    Task<int> GetInviteCountAsync(List<string> addressList, int beginTs);
    Task AddMilestoneVoucherClaimedAsync(MilestoneVoucherClaimedIndex record);
    Task<int> GetLastMilestoneLevelAsync(string address, string taskId);
    Task DeleteMilestoneVoucherClaimedRecordAsync(string taskId, string address, int level);
    Task<string> GetUserRegisterDomainByAddressAsync(string address);
    Task<string> GetAddressByUserIdAsync(string userId);
    Task<string> GetUserIdByAddressAsync(string address);
}

public class TasksProvider : ITasksProvider, ISingletonDependency
{
    private readonly INESTRepository<TasksIndex, string> _tasksIndexRepository;
    private readonly INESTRepository<TasksScoreIndex, string> _tasksScoreIndexRepository;
    private readonly INESTRepository<TasksScoreDetailIndex, string> _tasksScoreDetailIndexRepository;
    private readonly INESTRepository<SpinIndex, string> _spinIndexRepository;
    private readonly INESTRepository<TgBotLogIndex, string> _tgBotLogIndexRepository;
    private readonly INESTRepository<MilestoneVoucherClaimedIndex, string> _milestoneVoucherClaimedIndexRepository;
    private readonly IGraphQLClientFactory _graphQlClientFactory;
    private readonly ILogger<TasksProvider> _logger;
    private readonly IObjectMapper _objectMapper;

    public TasksProvider(
        INESTRepository<TasksIndex, string> tasksIndexRepository,
        INESTRepository<TasksScoreIndex, string> tasksScoreIndexRepository,
        INESTRepository<TasksScoreDetailIndex, string> tasksScoreDetailIndexRepository,
        IGraphQLClientFactory graphQlClientFactory,
        ILogger<TasksProvider> logger,
        IObjectMapper objectMapper,
        INESTRepository<SpinIndex, string> spinIndexRepository,
        INESTRepository<TgBotLogIndex, string> tgBotLogIndexRepository,
        INESTRepository<MilestoneVoucherClaimedIndex, string> milestoneVoucherClaimedIndexRepository)
    {
        _tasksIndexRepository = tasksIndexRepository;
        _tasksScoreIndexRepository = tasksScoreIndexRepository;
        _tasksScoreDetailIndexRepository = tasksScoreDetailIndexRepository;
        _graphQlClientFactory = graphQlClientFactory;
        _logger = logger;
        _objectMapper = objectMapper;
        _spinIndexRepository = spinIndexRepository;
        _tgBotLogIndexRepository = tgBotLogIndexRepository;
        _milestoneVoucherClaimedIndexRepository = milestoneVoucherClaimedIndexRepository;
    }

    public async Task<List<TasksDto>> GetTasksAsync(GetTasksInput input)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<TasksIndex>, QueryContainer>>
        {
            q => q.Term(i
                => i.Field(index => index.Address).Value(input.Address))
        };

        if (!input.TaskIdList.IsNullOrEmpty())
        {
            mustQuery.Add(q => q.Terms(i =>
                i.Field(f => f.TaskId).Terms(input.TaskIdList)));
        }

        if (!input.Date.IsNullOrEmpty())
        {
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.Date).Value(input.Date)));
        }

        QueryContainer Filter(QueryContainerDescriptor<TasksIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var res = await _tasksIndexRepository.GetListAsync(Filter, skip: 0, limit: 1000,
            sortType: SortOrder.Ascending, sortExp: o => o.CreatedTime);

        return _objectMapper.Map<List<TasksIndex>, List<TasksDto>>(res.Item2);
    }

    public async Task<TasksDto> ChangeTaskStatusAsync(ChangeTaskStatusInput input)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<TasksIndex>, QueryContainer>>
        {
            q => q.Term(i => i.Field(f => f.Address).Value(input.Address)),
            q => q.Term(i => i.Field(f => f.TaskId).Value(input.TaskId))
        };

        if (!input.Date.IsNullOrEmpty())
        {
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.Date).Value(input.Date)));
        }

        QueryContainer Filter(QueryContainerDescriptor<TasksIndex> f) => f.Bool(b => b.Must(mustQuery));

        var res = await _tasksIndexRepository.GetAsync(Filter);

        if (res != null)
        {
            if (input.Status <= res.Status)
            {
                return _objectMapper.Map<TasksIndex, TasksDto>(res);
            }

            res.Status = input.Status;
            res.UpdatedTime = DateTime.UtcNow;
            await _tasksIndexRepository.AddOrUpdateAsync(res);
            return _objectMapper.Map<TasksIndex, TasksDto>(res);
        }

        return null;
    }

    public async Task AddTasksAsync(List<TasksDto> tasks)
    {
        var tasksIndexList = _objectMapper.Map<List<TasksDto>, List<TasksIndex>>(tasks);
        await _tasksIndexRepository.BulkAddOrUpdateAsync(tasksIndexList);
    }

    public async Task<decimal> UpdateUserTaskScoreAsync(string address, decimal score)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<TasksScoreIndex>, QueryContainer>>
        {
            q => q.Term(i => i.Field(f => f.Id).Value(address))
        };

        QueryContainer Filter(QueryContainerDescriptor<TasksScoreIndex> f) => f.Bool(b => b.Must(mustQuery));

        var res = await _tasksScoreIndexRepository.GetAsync(Filter);
        if (res != null)
        {
            res.Score += score;
            res.UpdatedTime = DateTime.UtcNow;
            await _tasksScoreIndexRepository.AddOrUpdateAsync(res);
            return res.Score;
        }

        var index = new TasksScoreIndex
        {
            Id = address,
            Address = address,
            Score = score,
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow
        };

        await _tasksScoreIndexRepository.AddOrUpdateAsync(index);
        return index.Score;
    }

    public async Task AddTaskScoreDetailAsync(AddTaskScoreDetailInput input)
    {
        var index = new TasksScoreDetailIndex()
        {
            Id = input.Id,
            Address = input.Address,
            Score = input.Score,
            CreatedTime = DateTime.UtcNow
        };

        await _tasksScoreDetailIndexRepository.AddAsync(index);
    }

    public async Task<decimal> GetScoreAsync(string address)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<TasksScoreIndex>, QueryContainer>>
        {
            q => q.Term(i => i.Field(f => f.Id).Value(address))
        };

        QueryContainer Filter(QueryContainerDescriptor<TasksScoreIndex> f) => f.Bool(b => b.Must(mustQuery));

        var res = await _tasksScoreIndexRepository.GetAsync(Filter);
        if (res != null)
        {
            return res.Score;
        }

        return 0;
    }

    public async Task<List<UserReferralDto>> GetInviteRecordsToday(List<string> addressList)
    {
        var res = new List<UserReferralDto>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<UserReferralDto> list;
        do
        {
            list = await GetUserReferralRecordsAsync(addressList, skipCount, maxResultCount);
            var count = list.Count;
            res.AddRange(list);
            if (list.IsNullOrEmpty() || count < maxResultCount)
            {
                break;
            }

            skipCount += count;
        } while (!list.IsNullOrEmpty());

        DateTime currentUtcTime = DateTime.UtcNow;
        DateTime todayUtcStart = new DateTime(currentUtcTime.Year, currentUtcTime.Month, currentUtcTime.Day, 0, 0, 0,
            DateTimeKind.Utc);
        var dayInMillis = TimeHelper.ToUtcMilliSeconds(todayUtcStart);
        list = list.Where(i => i.CreateTime >= dayInMillis).ToList();
        return list;
    }


    public async Task<int> GetInviteCountAsync(List<string> addressList, int beginTs)
    {
        var res = new List<UserReferralDto>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<UserReferralDto> list;
        do
        {
            list = await GetUserReferralRecordsAsync(addressList, skipCount, maxResultCount);
            var count = list.Count;
            res.AddRange(list);
            if (list.IsNullOrEmpty() || count < maxResultCount)
            {
                break;
            }

            skipCount += count;
        } while (!list.IsNullOrEmpty());

        DateTime currentUtcTime = DateTime.UtcNow;
        DateTime todayUtcStart = new DateTime(currentUtcTime.Year, currentUtcTime.Month, currentUtcTime.Day, 0, 0, 0,
            DateTimeKind.Utc);
        var dayInMillis = TimeHelper.ToUtcMilliSeconds(todayUtcStart);
        list = list.Where(i => i.CreateTime >= beginTs).ToList();
        return list.Count;
    }

    private async Task<List<UserReferralDto>> GetUserReferralRecordsAsync(List<string> addressList, long skipCount = 0,
        long maxResultCount = 1000)
    {
        var res = await _graphQlClientFactory.GetClient(GraphQLClientEnum.PointPlatform)
            .SendQueryAsync<UserReferralQueryResultDto>(new GraphQLRequest
            {
                Query =
                    @"query($referrerList:[String!]!,$skipCount:Int!,$maxResultCount:Int!){
                    getUserReferralRecords(input: {referrerList:$referrerList,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalRecordCount
                        data{
                          domain
                          dappId
                          referrer
                          invitee
                          inviter
                          createTime
                    }
                }
            }",
                Variables = new
                {
                    referrerList = addressList, skipCount = skipCount, maxResultCount = maxResultCount
                }
            });
        return res.Data?.GetUserReferralRecords.Data;
    }


    public async Task<List<TaskScoreDetailDto>> GetScoreDetailByAddressAsync(string address)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<TasksScoreDetailIndex>, QueryContainer>>
        {
            q => q.Term(i => i.Field(f => f.Id).Value(address))
        };

        QueryContainer Filter(QueryContainerDescriptor<TasksScoreDetailIndex> f) => f.Bool(b => b.Must(mustQuery));

        var res = await _tasksScoreDetailIndexRepository.GetListAsync(Filter, skip: 0, limit: 10000,
            sortType: SortOrder.Ascending, sortExp: o => o.CreatedTime);

        return _objectMapper.Map<List<TasksScoreDetailIndex>, List<TaskScoreDetailDto>>(res.Item2);
    }


    public async Task<List<SpinDto>> GetUnfinishedSpinAsync(string address)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<SpinIndex>, QueryContainer>>
        {
            q => q.Term(i => i.Field(f => f.Address).Value(address)),
            q => q.Term(i => i.Field(f => f.Status).Value(SpinStatus.Created))
        };

        QueryContainer Filter(QueryContainerDescriptor<SpinIndex> f) => f.Bool(b => b.Must(mustQuery));

        var res = await _spinIndexRepository.GetListAsync(Filter);
        if (res.Item2.IsNullOrEmpty())
        {
            return new List<SpinDto>();
        }

        return _objectMapper.Map<List<SpinIndex>, List<SpinDto>>(res.Item2);
    }

    public async Task<int> GetFinishedSpinCountAsync(string address)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<SpinIndex>, QueryContainer>>
        {
            q => q.Term(i => i.Field(f => f.Address).Value(address)),
            q => q.Term(i => i.Field(f => f.Status).Value(SpinStatus.Finished))
            // q => q.Range(i => i.Field(f => f.ExpirationTime).GreaterThan(now))
        };

        QueryContainer Filter(QueryContainerDescriptor<SpinIndex> f) => f.Bool(b => b.Must(mustQuery));

        var res = await _spinIndexRepository.CountAsync(Filter);
        return (int)res.Count;
    }


    public async Task<decimal> GetTotalScoreFromTask(string address)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<TasksScoreDetailIndex>, QueryContainer>>
        {
            q => q.Term(i => i.Field(f => f.Address).Value(address))
        };

        QueryContainer Filter(QueryContainerDescriptor<TasksScoreDetailIndex> f) => f.Bool(b => b.Must(mustQuery));

        var res = await _tasksScoreDetailIndexRepository.GetListAsync(Filter, skip: 0, limit: 10000);
        if (res != null)
        {
            return res.Item2.Sum(i => i.Score);
        }

        return 0;
    }

    public async Task<decimal> GetConsumeScoreFromSpin(string address)
    {
        try
        {
            var res = await _graphQlClientFactory.GetClient(GraphQLClientEnum.SchrodingerClient)
                .SendQueryAsync<ConsumeScoreFromSpinDtoQueryDto>(new GraphQLRequest
                {
                    Query = @"query (
                    $address:String!
                ){
                  getConsumeScoreFromSpin(
                    input:{
                      address:$address
                    }
                  ){
                     score 
                   }
                }",
                    Variables = new
                    {
                        address = address
                    }
                });

            return res.Data.GetConsumeScoreFromSpin.Score;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "getConsumeScoreFromSpin query GraphQL error");
            return 0;
        }
    }

    public async Task<decimal> GetScoreFromSpinRewardAsync(string address)
    {
        try
        {
            var res = await _graphQlClientFactory.GetClient(GraphQLClientEnum.SchrodingerClient)
                .SendQueryAsync<ScoreFromSpinDtoRewardQueryDto>(new GraphQLRequest
                {
                    Query = @"query (
                    $address:String!
                ){
                  getScoreFromSpinReward(
                    input:{
                      address:$address
                    }
                  ){
                     score
                  }
                }",
                    Variables = new
                    {
                        address = address
                    }
                });
            return res.Data.GetScoreFromSpinReward.Score;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "getScoreFromSpin query GraphQL error");
            return 0;
        }
    }

    public async Task<bool> IsSpinFinished(string seed)
    {
        try
        {
            var res = await _graphQlClientFactory.GetClient(GraphQLClientEnum.SchrodingerClient)
                .SendQueryAsync<GetSpinResultQueryDto>(new GraphQLRequest
                {
                    Query = @"query (
                    $seed:String!
                ){
                  getSpinResult(
                    input:{
                      seed:$seed
                    }
                  ){
                     address,
                     spinId,
                     name
                  }
                }",
                    Variables = new
                    {
                        seed = seed
                    }
                });
            return !res.Data.GetSpinResult.SpinId.IsNullOrEmpty();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "getSpinResult query GraphQL error");
            return false;
        }
    }

    public async Task FinishSpinAsync(string seed)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<SpinIndex>, QueryContainer>>
        {
            q => q.Term(i => i.Field(f => f.Seed).Value(seed))
        };

        QueryContainer Filter(QueryContainerDescriptor<SpinIndex> f) => f.Bool(b => b.Must(mustQuery));

        var res = await _spinIndexRepository.GetAsync(Filter);

        if (res != null)
        {
            res.Status = SpinStatus.Finished;
            await _spinIndexRepository.AddOrUpdateAsync(res);
        }
    }

    public async Task AddSpinAsync(AddSpinInput input)
    {
        var index = new SpinIndex
        {
            Id = input.Address + "_" + input.Seed,
            Address = input.Address,
            Seed = input.Seed,
            ConsumeScore = 100,
            Status = SpinStatus.Created,
            Signature = input.Signature,
            ExpirationTime = input.ExpirationTime,
            CreatedTime = DateTime.UtcNow.ToUtcSeconds()
        };

        await _spinIndexRepository.AddAsync(index);
    }


    public async Task<VoucherAdoptionDto> GetVoucherAdoptionAsync(string voucherId)
    {
        try
        {
            var res = await _graphQlClientFactory.GetClient(GraphQLClientEnum.SchrodingerClient)
                .SendQueryAsync<GetVoucherAdoptionQueryDto>(new GraphQLRequest
                {
                    Query = @"query (
                    $voucherId:String!
                ){
                  getVoucherAdoption(
                    input:{
                      voucherId:$voucherId
                    }
                  ){
                     voucherId,
                     rarity,
                     rank
                  }
                }",
                    Variables = new
                    {
                        voucherId = voucherId
                    }
                });
            return res.Data.GetVoucherAdoption;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "getScoreFromSpin query GraphQL error");
            return null;
        }
    }

    public async Task<SpinRewardConfigDto> GetSpinRewardConfigAsync()
    {
        try
        {
            var res = await _graphQlClientFactory.GetClient(GraphQLClientEnum.SchrodingerClient)
                .SendQueryAsync<SpinRewardConfigQueryDto>(new GraphQLRequest
                {
                    Query = @"query 
                  {
                  getLatestSpinRewardConfig 
                  {
                     rewardList {
                        name
                        amount
                      }
                  }
                }"
                });
            return res.Data.GetLatestSpinRewardConfig;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "getLatestSpinRewardConfig query GraphQL error");
            return null;
        }
    }

    public async Task AddTgBotLogAsync(LogTgBotInput input)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<TgBotLogIndex>, QueryContainer>>
        {
            q => q.Term(i => i.Field(f => f.Id).Value(input.UserId))
        };

        QueryContainer Filter(QueryContainerDescriptor<TgBotLogIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var index = await _tgBotLogIndexRepository.GetAsync(Filter);
        if (index == null || index.Id.IsNullOrEmpty())
        {
            index = _objectMapper.Map<LogTgBotInput, TgBotLogIndex>(input);
            index.Id = input.UserId;
            index.RegisterTime = input.LoginTime;
            await _tgBotLogIndexRepository.AddAsync(index);
        }
        else
        {
            index.LoginTime = input.LoginTime;
            index.Score = input.Score;
            index.ExtraData = input.ExtraData;
            index.Language = input.Language;
            // index.Address = input.Address;
            index.Username = input.Username;
            await _tgBotLogIndexRepository.UpdateAsync(index);
        }
    }

    public async Task AddMilestoneVoucherClaimedAsync(MilestoneVoucherClaimedIndex record)
    {
        await _milestoneVoucherClaimedIndexRepository.AddAsync(record);
    }
    
    public async Task DeleteMilestoneVoucherClaimedRecordAsync(string taskId, string address, int level)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<MilestoneVoucherClaimedIndex>, QueryContainer>>
        {
            q => q.Term(i => i.Field(f => f.Address).Value(address)),
            q => q.Term(i => i.Field(f => f.TaskId).Value(taskId)),
            q => q.Range(i => i.Field(f => f.Level).GreaterThan(level))
        };

        QueryContainer Filter(QueryContainerDescriptor<MilestoneVoucherClaimedIndex> f) =>
            f.Bool(b => b.Must(mustQuery));
        
        var res = await _milestoneVoucherClaimedIndexRepository.GetListAsync(Filter);
        if (res != null && !res.Item2.IsNullOrEmpty())
        {
            foreach (var item in res.Item2)
            {
                await _milestoneVoucherClaimedIndexRepository.DeleteAsync(item);
            }
        }
    }

    public async Task<int> GetLastMilestoneLevelAsync(string address, string taskId)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<MilestoneVoucherClaimedIndex>, QueryContainer>>
        {
            q => q.Term(i => i.Field(f => f.Address).Value(address)),
            q => q.Term(i => i.Field(f => f.TaskId).Value(taskId))
        };

        QueryContainer Filter(QueryContainerDescriptor<MilestoneVoucherClaimedIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var res = await _milestoneVoucherClaimedIndexRepository.GetListAsync(Filter, skip: 0, limit: 10000,
            sortType: SortOrder.Descending, sortExp: o => o.Level);
        if (res != null && !res.Item2.IsNullOrEmpty())
        {
            return res.Item2.First().Level;
        }

        return 0;
    }
    
    public async Task<string> GetUserRegisterDomainByAddressAsync(string address)
    {
        var res = await _graphQlClientFactory.GetClient(GraphQLClientEnum.PointPlatform)
            .SendQueryAsync<DomainUserRelationShipQuery>(new GraphQLRequest
            {
                Query =
                    @"query($domainIn:[String!]!,$addressIn:[String!]!,$dappNameIn:[String!]!,$skipCount:Int!,$maxResultCount:Int!){
                    queryUserAsync(input: {domainIn:$domainIn,addressIn:$addressIn,dappNameIn:$dappNameIn,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                        totalRecordCount
                        data {
                          id
                          domain
                          address
                          dappName
                          createTime
                        }
                }
            }",
                Variables = new
                {
                    domainIn = new List<string>(), dappNameIn = new List<string>(),
                    addressIn = new List<string>() { address }, skipCount = 0, maxResultCount = 1
                }
            });
        var ans = res.Data?.QueryUserAsync.Data;
        if (ans == null || ans.Count == 0)
        {
            return "";
        }

        return ans[0].Domain;
    }

    public async Task<string> GetAddressByUserIdAsync(string userId)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<TgBotLogIndex>, QueryContainer>>
        {
            q => q.Term(i => i.Field(f => f.Id).Value(userId))
        };
        
        QueryContainer Filter(QueryContainerDescriptor<TgBotLogIndex> f) => f.Bool(b => b.Must(mustQuery));

        var res = await _tgBotLogIndexRepository.GetAsync(Filter);

        if (res != null)
        {
            return res.Address;
        }

        return "";
    }
    
    public async Task<string> GetUserIdByAddressAsync(string address)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<TgBotLogIndex>, QueryContainer>>
        {
            q => q.Term(i => i.Field(f => f.Address).Value(address))
        };

        QueryContainer Filter(QueryContainerDescriptor<TgBotLogIndex> f) => f.Bool(b => b.Must(mustQuery));
        
        var res = await _tgBotLogIndexRepository.GetListAsync(Filter, skip: 0, limit: 10,
            sortType: SortOrder.Ascending, sortExp: o => o.RegisterTime);
        
        if (res != null && !res.Item2.IsNullOrEmpty())
        {
            return res.Item2.First().UserId;
        }
        
        return "";
    }
}