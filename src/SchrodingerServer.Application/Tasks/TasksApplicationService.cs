using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Tasks.Dtos;
using SchrodingerServer.Tasks.Provider;
using SchrodingerServer.Users;
using SchrodingerServer.Users.Index;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Tasks;

public class TasksApplicationService : ApplicationService, ITasksApplicationService
{
    private readonly ITasksProvider _tasksProvider;
    private readonly ILogger<TasksApplicationService> _logger;
    private IOptionsMonitor<TasksOptions> _tasksOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly IUserActionProvider _userActionProvider;
    private readonly IAbpDistributedLock _distributedLock;
    private const string LoginTaskId = "login";
    private const string InviteTaskId = "invite";
    
    public TasksApplicationService(
        ITasksProvider tasksProvider, 
        ILogger<TasksApplicationService> logger,
        IOptionsMonitor<TasksOptions> tasksOptions,
        IObjectMapper objectMapper, 
        IUserActionProvider userActionProvider, 
        IAbpDistributedLock distributedLock)
    {
        _tasksProvider = tasksProvider;
        _logger = logger;
        _tasksOptions = tasksOptions;
        _objectMapper = objectMapper;
        _userActionProvider = userActionProvider;
        _distributedLock = distributedLock;
    }

    public async Task<GetTaskListOutput> GetTaskListAsync(GetTaskListInput input)
    {
        var tasksOptions = _tasksOptions.CurrentValue;
        var taskList = tasksOptions.TaskList;
        var dailyTasks = taskList.Where(i => i.Type == TaskType.Daily).ToList();
        var socialTasks = taskList.Where(i => i.Type == TaskType.Social).ToList();
        var accomplishmentTasks = taskList.Where(i => i.Type == TaskType.Accomplishment).ToList();
        
        var dailyTaskList = await GetDailyTasksAsync(dailyTasks, input.Address);
        dailyTaskList = dailyTaskList.OrderBy(i => GetSortOrder(i.Status)).ThenBy(i => i.Name).ToList();
        
        var socialTaskList = await GetOtherTasksAsync(socialTasks, input.Address);
        socialTaskList = socialTaskList.OrderBy(i => GetSortOrder(i.Status)).ThenBy(i => i.Name).ToList();
        
        var accomplishmentTaskList = await GetOtherTasksAsync(accomplishmentTasks, input.Address);
        accomplishmentTaskList = accomplishmentTaskList.OrderBy(i => GetSortOrder(i.Status)).ThenBy(i => i.Name).ToList();
        
        var res = await _tasksProvider.GetScoreAsync(input.Address);
        return new GetTaskListOutput
        {
            DailyTasks = _objectMapper.Map<List<TasksDto>, List<TaskData>>(dailyTaskList),
            SocialTasks = _objectMapper.Map<List<TasksDto>, List<TaskData>>(socialTaskList),
            AccomplishmentTasks = _objectMapper.Map<List<TasksDto>, List<TaskData>>(accomplishmentTaskList),
            FishScore = res
        };
    }

    private async Task<List<TasksDto>> GetDailyTasksAsync(List<TaskConfig> taskInfoList, string address)
    {
        var bizDate = DateTime.UtcNow.ToString(TimeHelper.Pattern);
        var dailyTaskList = await _tasksProvider.GetTasksAsync(new GetTasksInput
        {
            Address = address,
            Date = bizDate,
            TaskIdList = taskInfoList.Select(i => i.TaskId).ToList()
        });

        if (dailyTaskList.IsNullOrEmpty())
        {
            dailyTaskList = _objectMapper.Map<List<TaskConfig>, List<TasksDto>>(taskInfoList);
            foreach (var tasksDto in dailyTaskList)
            { 
                tasksDto.CreatedTime = DateTime.UtcNow;
                tasksDto.UpdatedTime = DateTime.UtcNow;
                tasksDto.Date = bizDate;
                tasksDto.Status = tasksDto.TaskId == LoginTaskId ? UserTaskStatus.Finished : UserTaskStatus.Created;
                tasksDto.Address = address;
                tasksDto.Id = bizDate + address + tasksDto.TaskId;
            }
            await _tasksProvider.AddTasksAsync(dailyTaskList);
            _logger.LogDebug("add task for address:{address}, tasks:{tasks}", address, dailyTaskList);
        }
        
        foreach (var tasksDto in dailyTaskList)
        {
            if (tasksDto.TaskId == InviteTaskId && tasksDto.Status == UserTaskStatus.Created)
            {
                var inviterRecordsToday = await _tasksProvider.GetInviteRecordsToday(new List<string> { tasksDto.Address });
                if (!inviterRecordsToday.IsNullOrEmpty())
                {
                    tasksDto.Status = UserTaskStatus.Finished;
                    await _tasksProvider.AddTasksAsync(new List<TasksDto> { tasksDto });
                }
            }
        }
        
        return dailyTaskList;
    }
    
    private async Task<List<TasksDto>> GetOtherTasksAsync(List<TaskConfig> taskInfoList, string address)
    {
        var taskList = await _tasksProvider.GetTasksAsync(new GetTasksInput
        {
            Address = address,
            TaskIdList = taskInfoList.Select(i => i.TaskId).ToList()
        });

        var socialTaskId = taskInfoList.Select(i => i.TaskId).ToList();
        var existTaskId = taskList.Select(i => i.TaskId).ToList();
        
        var taskNotAdded = socialTaskId.Except(existTaskId).ToList();

        if (!taskNotAdded.IsNullOrEmpty())
        {
            var newTasks = new List<TasksDto>();
            foreach (var taskId in taskNotAdded)
            {
                var taskDto = new TasksDto
                {
                    Id = address + taskId,
                    CreatedTime = DateTime.UtcNow,
                    UpdatedTime = DateTime.UtcNow,
                    Status = UserTaskStatus.Created,
                    Address = address,
                    Name = taskInfoList.First(i => i.TaskId == taskId).Name
                };
                
                newTasks.Add(taskDto);
            }
            await  _tasksProvider.AddTasksAsync(newTasks);
            _logger.LogDebug("add task for address:{address}, tasks:{tasks}", address, newTasks);
            taskList.AddRange(newTasks);
        }
        
        return taskList;
    }
    
    // private async Task<List<TasksDto>> GetAccomplishmentTasksAsync(List<TaskConfig> taskInfoList, string address)
    // {
    //     var taskList = await _tasksProvider.GetTasksAsync(new GetTasksInput
    //     {
    //         Address = address,
    //         TaskIdList = taskInfoList.Select(i => i.TaskId).ToList()
    //     });
    //
    //     if (taskList.IsNullOrEmpty())
    //     {
    //         var newTasks = _objectMapper.Map<List<TaskConfig>, List<TasksDto>>(taskInfoList);
    //         foreach (var tasksDto in newTasks)
    //         { 
    //             tasksDto.CreatedTime = DateTime.UtcNow;
    //             tasksDto.UpdatedTime = DateTime.UtcNow;
    //             tasksDto.Status = UserTaskStatus.Created;
    //             tasksDto.Address = address;
    //             tasksDto.Id = address + tasksDto.TaskId;
    //         }
    //         await _tasksProvider.AddTasksAsync(newTasks);
    //         return newTasks;
    //     }
    //     
    //     return taskList;
    // }

    public async Task<TaskData> FinishAsync(FinishInput input)
    {
        var currentAddress = await _userActionProvider.GetCurrentUserAddressAsync();
        if (currentAddress.IsNullOrEmpty())
        {
            _logger.LogError("Get current address failed");
            throw new UserFriendlyException("Invalid user");
        }
        
        var key = currentAddress + input.TaskId;
        
        var taskOption = _tasksOptions.CurrentValue;
        var taskList =  taskOption.TaskList;
        var dailyTasks = taskList.Where(i => i.Type == TaskType.Daily).ToList();
        var dailyTaskId = dailyTasks.Select(i => i.TaskId).ToList();
        if (dailyTaskId.Contains(input.TaskId))
        {
            key += DateTime.UtcNow.ToString(TimeHelper.Pattern);;
        }
        
        await using var handle =
            await _distributedLock.TryAcquireAsync(key);
        
        if (handle == null)
        {
            _logger.LogError("get lock failed");
            throw new UserFriendlyException("please try later");
        }

        var res = await _tasksProvider.ChangeTaskStatusAsync(new ChangeTaskStatusInput
        {
            Address = currentAddress,
            TaskId = input.TaskId,
            Status = UserTaskStatus.Finished
        });

        if (res == null)
        {
            _logger.LogError("user task not exist, address: {address}, task:{task}", currentAddress, input.TaskId);
            throw new UserFriendlyException("user task not exist");
        }
        
        if (res.Status != UserTaskStatus.Finished)
        {
            _logger.LogError("finish task failed, address: {address}, task:{task}, status:{status}", currentAddress, input.TaskId, res.Status);
            throw new UserFriendlyException("finish failed");
        }
        
        return _objectMapper.Map<TasksDto, TaskData>(res);
    }
    
    public async Task<ClaimOutput> ClaimAsync(ClaimInput input)
    {
        var currentAddress = await _userActionProvider.GetCurrentUserAddressAsync();
        if (currentAddress.IsNullOrEmpty())
        {
            _logger.LogError("Get current address failed");
            throw new UserFriendlyException("Invalid user");
        }
        
        var today = DateTime.UtcNow.ToString(TimeHelper.Pattern);
        
        var date = "";
        var key = currentAddress + input.TaskId;

        var taskOption = _tasksOptions.CurrentValue;
        var taskList =  taskOption.TaskList;
        var dailyTasks = taskList.Where(i => i.Type == TaskType.Daily).ToList();
        var dailyTaskId = dailyTasks.Select(i => i.TaskId).ToList();
        if (dailyTaskId.Contains(input.TaskId))
        {
            date = today;
            key += date;
        }
        
        await using var handle =
            await _distributedLock.TryAcquireAsync(key);
        
        if (handle == null)
        {
            _logger.LogError("get lock failed");
            throw new UserFriendlyException("please try later");
        }
        
        var tasks = await _tasksProvider.GetTasksAsync(new GetTasksInput
        {
            Address = currentAddress,
            TaskIdList = new List<string>{input.TaskId},
            Date = date 
        });
        
        var taskInfo = tasks.FirstOrDefault();
        if (taskInfo == null)
        {
            _logger.LogError("user task not exist, address: {address}, task:{task}", currentAddress, input.TaskId);
            throw new UserFriendlyException("user task not exist");
        }
        
        if (taskInfo.Status != UserTaskStatus.Finished)
        {
            _logger.LogError("invalid status, address:{address}, status:{status}, task:{task}", currentAddress, taskInfo.Status, input.TaskId);
            throw new UserFriendlyException("invalid status");
        }

        var res = await _tasksProvider.ChangeTaskStatusAsync(new ChangeTaskStatusInput
        {
            Address = currentAddress,
            TaskId = input.TaskId,
            Status = UserTaskStatus.Claimed
        });

        if (res == null)
        {
            _logger.LogError("user task not exist, address: {address}, task:{task}", currentAddress, input.TaskId);
            throw new UserFriendlyException("user task not exist");
        }
        
        if (res.Status != UserTaskStatus.Claimed)
        {
            _logger.LogError("claim task failed, address: {address}, task:{task}, status:{status}", currentAddress, input.TaskId, res.Status);
            throw new UserFriendlyException("claim failed");
        }
        
        var score = taskList.Where(i => i.TaskId == input.TaskId).Select(i => i.Score).FirstOrDefault();
        if (score == 0)
        {
            _logger.LogError("invalid taskId, address: {address}, task:{task}", currentAddress, input.TaskId);
            throw new UserFriendlyException("invalid taskId");
        }
        
        await _tasksProvider.AddTaskScoreDetailAsync(new AddTaskScoreDetailInput
        {
            Address = currentAddress,
            TaskId = input.TaskId,
            Score = score,
            Id = key
        });
        
        var newScore = await _tasksProvider.UpdateUserTaskScoreAsync(currentAddress, score);
        
        var output = _objectMapper.Map<TasksDto, ClaimOutput>(res);
        output.FishScore = newScore;
        return output;
    }

    public async Task<GetScoreOutput> GetScoreAsync(GetScoreInput input)
    {
        var res = await _tasksProvider.GetScoreAsync(input.Address);
        return new GetScoreOutput
        {
            FishScore = res
        };
    }
    
    private static int GetSortOrder(UserTaskStatus status)
    {
        return status switch
        {
            UserTaskStatus.Finished => 0, 
            UserTaskStatus.Created => 1,
            UserTaskStatus.Claimed => 2, 
            _ => 3 
        };
    }
    
}