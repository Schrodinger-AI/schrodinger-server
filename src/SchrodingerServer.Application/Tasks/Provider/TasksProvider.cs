using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.Logging;
using Nest;
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
    Task<decimal> UpdateUserTaskScoreAsync(string address, decimal score);
    Task AddTaskScoreDetailAsync(AddTaskScoreDetailInput input);
    Task<decimal> GetScoreAsync(string address);
}

public class TasksProvider : ITasksProvider, ISingletonDependency
{
    private readonly INESTRepository<TasksIndex, string> _tasksIndexRepository;
    private readonly INESTRepository<TasksScoreIndex, string> _tasksScoreIndexRepository;
    private readonly INESTRepository<TasksScoreDetailIndex, string> _tasksScoreDetailIndexRepository;
    private readonly IGraphQLClientFactory _graphQlClientFactory;
    private readonly ILogger<TasksProvider> _logger;
    private readonly IObjectMapper _objectMapper;
    
    public TasksProvider(
        INESTRepository<TasksIndex, string> tasksIndexRepository, 
        INESTRepository<TasksScoreIndex, string> tasksScoreIndexRepository, 
        INESTRepository<TasksScoreDetailIndex, string> tasksScoreDetailIndexRepository, 
        IGraphQLClientFactory graphQlClientFactory, 
        ILogger<TasksProvider> logger,
        IObjectMapper objectMapper)
    {
        _tasksIndexRepository = tasksIndexRepository;
        _tasksScoreIndexRepository = tasksScoreIndexRepository;
        _tasksScoreDetailIndexRepository = tasksScoreDetailIndexRepository;
        _graphQlClientFactory = graphQlClientFactory;
        _logger = logger;
        _objectMapper = objectMapper;
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
        
        return  _objectMapper.Map<List<TasksIndex>, List<TasksDto>>(res.Item2);
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
}