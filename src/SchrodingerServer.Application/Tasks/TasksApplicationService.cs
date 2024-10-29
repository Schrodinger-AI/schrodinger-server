using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Types;
using Google.Protobuf;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Schrodinger;
using SchrodingerServer.Adopts.provider;
using SchrodingerServer.Common;
using SchrodingerServer.Common.AElfSdk;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Message.Dtos;
using SchrodingerServer.Message.Provider;
using SchrodingerServer.Options;
using SchrodingerServer.Tasks.Dtos;
using SchrodingerServer.Tasks.Provider;
using SchrodingerServer.Users;
using SchrodingerServer.Users.Index;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
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
    private readonly IMessageProvider _messageProvider;
    private readonly IAdoptGraphQLProvider _adoptGraphQlProvider;
    private readonly IOptionsMonitor<LevelOptions> _levelOptions;
    private readonly IDistributedCache<SpinOutputCache> _spinCache;
    private readonly ISecretProvider _secretProvider;
    private readonly ChainOptions _chainOptions;
    private IOptionsMonitor<SpinRewardOptions> _spinRewardOptions;
    private readonly IDistributedCache<MilestoneTaskCache> _milestoneTaskCache;
    private readonly IContractProvider _contractProvider;

    private const string LoginTaskId = "login";
    private const string InviteTaskId = "invite";
    private const string TradeTaskId = "trade";
    private const string AdoptTaskId = "adopt";
    private const string AdoptOnceTaskId = "adoptOnce";
    private const string CatEmojiTaskId = "catEmoji";
    private const string AdoptMilestoneTaskId = "adoptMilestone";
    private const string InviteMilestoneTaskId = "inviteMilestone";
    private const int MilestoneStartFrom = 1730160000;
    private const string DefaultTick = "SGR";

    public TasksApplicationService(
        ITasksProvider tasksProvider,
        ILogger<TasksApplicationService> logger,
        IOptionsMonitor<TasksOptions> tasksOptions,
        IObjectMapper objectMapper,
        IUserActionProvider userActionProvider,
        IAbpDistributedLock distributedLock,
        IMessageProvider messageProvider,
        IAdoptGraphQLProvider adoptGraphQlProvider,
        IOptionsMonitor<LevelOptions> levelOptions,
        IDistributedCache<SpinOutputCache> spinCache,
        ISecretProvider secretProvider,
        IOptionsMonitor<ChainOptions> chainOptions,
        IOptionsMonitor<SpinRewardOptions> spinRewardOptions,
        IDistributedCache<MilestoneTaskCache> milestoneTaskCache,
        IContractProvider contractProvider
    )
    {
        _tasksProvider = tasksProvider;
        _logger = logger;
        _tasksOptions = tasksOptions;
        _objectMapper = objectMapper;
        _userActionProvider = userActionProvider;
        _distributedLock = distributedLock;
        _messageProvider = messageProvider;
        _adoptGraphQlProvider = adoptGraphQlProvider;
        _levelOptions = levelOptions;
        _spinCache = spinCache;
        _secretProvider = secretProvider;
        _chainOptions = chainOptions.CurrentValue;
        _spinRewardOptions = spinRewardOptions;
        _milestoneTaskCache = milestoneTaskCache;
        _contractProvider = contractProvider;
    }

    public async Task<GetTaskListOutput> GetTaskListAsync(GetTaskListInput input)
    {
        _logger.LogDebug("GetTaskListAsync");

        var currentAddress = await _userActionProvider.GetCurrentUserAddressAsync();
        // var currentAddress = input.Address;
        if (currentAddress.IsNullOrEmpty())
        {
            _logger.LogError("Get current address failed");
            throw new UserFriendlyException("Invalid user");
        }

        _logger.LogDebug("GetTaskListAsync for address, address:{address}", currentAddress);

        var tasksOptions = _tasksOptions.CurrentValue;

        _logger.LogDebug("tasksOptions, :{tasksOptions}", JsonConvert.SerializeObject(tasksOptions));

        var taskList = tasksOptions.TaskList;
        var dailyTasks = taskList.Where(i => i.Type == TaskType.Daily).ToList();
        var socialTasks = taskList.Where(i => i.Type == TaskType.Social).ToList();
        var accomplishmentTasks = taskList.Where(i => i.Type == TaskType.Accomplishment).ToList();

        var dailyTaskList = await GetDailyTasksAsync(dailyTasks, currentAddress);
        _logger.LogDebug("GetDailyTaskList, list:{dailyTaskList}", JsonConvert.SerializeObject(dailyTaskList));
        dailyTaskList = dailyTaskList.OrderBy(i => GetSortOrder(i.Status)).ThenBy(i => i.Name).ToList();

        var socialTaskList = await GetOtherTasksAsync(socialTasks, currentAddress);
        _logger.LogDebug("GetSocialTaskList, list:{socialTaskList}", JsonConvert.SerializeObject(socialTaskList));
        socialTaskList = socialTaskList.OrderBy(i => GetSortOrder(i.Status)).ThenBy(i => i.Name).ToList();

        var accomplishmentTaskList = await GetOtherTasksAsync(accomplishmentTasks, currentAddress);
        _logger.LogDebug("GetAccomplishmentTaskList, list:{accomplishmentTaskList}",
            JsonConvert.SerializeObject(accomplishmentTaskList));
        accomplishmentTaskList =
            accomplishmentTaskList.OrderBy(i => GetSortOrder(i.Status)).ThenBy(i => i.Name).ToList();

        var nowUtc = DateTime.UtcNow;
        var tomorrowUtcZero =
            new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
        var timeDifference = tomorrowUtcZero - nowUtc;

        return new GetTaskListOutput
        {
            DailyTasks = _objectMapper.Map<List<TasksDto>, List<TaskData>>(dailyTaskList),
            SocialTasks = _objectMapper.Map<List<TasksDto>, List<TaskData>>(socialTaskList),
            AccomplishmentTasks = _objectMapper.Map<List<TasksDto>, List<TaskData>>(accomplishmentTaskList),
            Countdown = (int)Math.Ceiling(timeDifference.TotalSeconds)
        };
    }

    private async Task<List<TasksDto>> GetDailyTasksAsync(List<TaskConfig> taskInfoList, string address)
    {
        var bizDate = DateTime.UtcNow.ToString(TimeHelper.Pattern);
        // var bizDate = "20241017";
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
                tasksDto.Id = tasksDto.TaskId + "_" + address + "_" + bizDate;
            }

            await _tasksProvider.AddTasksAsync(dailyTaskList);
            _logger.LogDebug("add task for address:{address}, tasks:{tasks}", address, dailyTaskList);
        }

        _logger.LogDebug("GetDailyTaskList, task:{task}", JsonConvert.SerializeObject(dailyTaskList));
        var nowUtc = DateTime.UtcNow;
        var todayBegin = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc);
        var todayEnd = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 23, 59, 59, DateTimeKind.Utc);
        foreach (var tasksDto in dailyTaskList)
        {
            tasksDto.Link = taskInfoList.First(i => i.TaskId == tasksDto.TaskId).Link;
            tasksDto.Name = taskInfoList.First(i => i.TaskId == tasksDto.TaskId).Name;
            tasksDto.LinkType = taskInfoList.First(i => i.TaskId == tasksDto.TaskId).LinkType;

            if (tasksDto.TaskId == InviteTaskId && tasksDto.Status == UserTaskStatus.Created)
            {
                _logger.LogDebug("check invite for address:{address}", tasksDto.Address);
                var inviterRecordsToday =
                    await _tasksProvider.GetInviteRecordsToday(new List<string> { tasksDto.Address });
                if (!inviterRecordsToday.IsNullOrEmpty())
                {
                    tasksDto.Status = UserTaskStatus.Finished;
                    await _tasksProvider.AddTasksAsync(new List<TasksDto> { tasksDto });
                }
            }

            if (tasksDto.TaskId == TradeTaskId)
            {
                tasksDto.Name = "Trade a Cat (1/1)";
                if (tasksDto.Status == UserTaskStatus.Created)
                {
                    _logger.LogDebug("check trade for address:{address}", tasksDto.Address);

                    var chainId = _levelOptions.CurrentValue.ChainIdForReal;
                    var fullAddress = FullAddressHelper.ToFullAddress(tasksDto.Address, chainId);

                    var tradeRecordsToday = await _messageProvider.GetSchrodingerSoldListAsync(
                        new GetSchrodingerSoldListInput
                        {
                            Buyer = fullAddress,
                            FilterSymbol = "SGR",
                            MaxResultCount = 10,
                            SkipCount = 0,
                            Address = "",
                            TimestampMin = todayBegin.ToUtcMilliSeconds()
                        });

                    var cnt = tradeRecordsToday.TotalRecordCount;
                    if (cnt > 0)
                    {
                        tasksDto.Status = UserTaskStatus.Finished;
                        await _tasksProvider.AddTasksAsync(new List<TasksDto> { tasksDto });
                    }
                    else
                    {
                        tasksDto.Name = "Trade a Cat (0/1)";
                    }
                }
            }

            if (tasksDto.TaskId == AdoptTaskId)
            {
                tasksDto.Name = "Adopt Gen9 Cats (3/3)";
                if (tasksDto.Status == UserTaskStatus.Created)
                {
                    _logger.LogDebug("check adopt for address:{address}", tasksDto.Address);
                    var res = await _adoptGraphQlProvider.GetAdoptInfoByTime(todayBegin.ToUtcSeconds(),
                        todayEnd.ToUtcSeconds());
                    var gen9AdoptByCurrentAddress =
                        res.Where(i => i.Adopter == tasksDto.Address && i.Gen == 9).ToList();
                    var cnt = gen9AdoptByCurrentAddress.Count;
                    _logger.LogDebug("check adopt for address:{address}, adopt times: {cnt}", tasksDto.Address, cnt);

                    if (cnt >= 3)
                    {
                        tasksDto.Status = UserTaskStatus.Finished;
                        await _tasksProvider.AddTasksAsync(new List<TasksDto> { tasksDto });
                    }
                    else
                    {
                        tasksDto.Name = "Adopt Gen9 Cats (" + cnt + "/3)";
                    }
                }
            }

            if (tasksDto.TaskId == AdoptOnceTaskId)
            {
                tasksDto.Name = "Adopt Gen9 Cats (1/1)";
                if (tasksDto.Status == UserTaskStatus.Created)
                {
                    _logger.LogDebug("check adopt for address:{address}", tasksDto.Address);
                    var res = await _adoptGraphQlProvider.GetAdoptInfoByTime(todayBegin.ToUtcSeconds(),
                        todayEnd.ToUtcSeconds());
                    var gen9AdoptByCurrentAddress =
                        res.Where(i => i.Adopter == tasksDto.Address && i.Gen == 9).ToList();
                    var cnt = gen9AdoptByCurrentAddress.Count;
                    _logger.LogDebug("check adopt for address:{address}, adopt times: {cnt}", tasksDto.Address, cnt);

                    if (cnt >= 1)
                    {
                        tasksDto.Status = UserTaskStatus.Finished;
                        await _tasksProvider.AddTasksAsync(new List<TasksDto> { tasksDto });
                    }
                    else
                    {
                        tasksDto.Name = "Adopt Gen9 Cats (" + cnt + "/1)";
                    }
                }
            }
        }

        return dailyTaskList;
    }

    private async Task<List<TasksDto>> GetOtherTasksAsync(List<TaskConfig> taskInfoList, string address)
    {
        if (taskInfoList.IsNullOrEmpty())
        {
            return new List<TasksDto>();
        }

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
                    Id = taskId + "_" + address,
                    CreatedTime = DateTime.UtcNow,
                    UpdatedTime = DateTime.UtcNow,
                    Status = UserTaskStatus.Created,
                    Address = address,
                    TaskId = taskId,
                    Name = taskInfoList.First(i => i.TaskId == taskId).Name,
                    Score = taskInfoList.First(i => i.TaskId == taskId).Score
                };

                newTasks.Add(taskDto);
            }

            await _tasksProvider.AddTasksAsync(newTasks);
            _logger.LogDebug("add task for address:{address}, tasks:{tasks}", address, newTasks);
            taskList.AddRange(newTasks);
        }


        foreach (var tasksDto in taskList)
        {
            tasksDto.Link = taskInfoList.First(i => i.TaskId == tasksDto.TaskId).Link;
            tasksDto.Name = taskInfoList.First(i => i.TaskId == tasksDto.TaskId).Name;
            tasksDto.LinkType = taskInfoList.First(i => i.TaskId == tasksDto.TaskId).LinkType;
        }

        return taskList;
    }

    private async Task<List<TasksDto>> GetAccomplishmentTasksAsync(List<TaskConfig> taskInfoList, string address)
    {
        var taskList = new List<TasksDto>();

        foreach (var taskInfo in taskInfoList)
        {
            if (taskInfo.TaskId == AdoptMilestoneTaskId)
            {
                var tasksDto = new TasksDto();

                _logger.LogDebug("check adopt milestone for address:{address}", address);
                var res = await _adoptGraphQlProvider.GetAdoptInfoByTime(MilestoneStartFrom,
                    TimeHelper.GetTimeStampInSeconds());
                var gen9AdoptByCurrentAddress =
                    res.Where(i => i.Adopter == address && i.Gen == 9).ToList();
                var cnt = gen9AdoptByCurrentAddress.Count;
                _logger.LogDebug("check adopt milestone for address:{address}, adopt times: {cnt}", address, cnt);

                var lastClaimLevel = await GetMilestoneTaskLevelAsync(address, taskInfo.TaskId);
                var nextLevel = GetNextMilestoneLevel(lastClaimLevel, taskInfo);

                if (cnt >= nextLevel)
                {
                    tasksDto.Status = UserTaskStatus.Finished;
                }
                else
                {
                    tasksDto.Status = UserTaskStatus.Created;
                }

                tasksDto.Name = "Adopt Gen9 Cats (" + cnt + "/" + nextLevel + ")";

                taskList.Add(tasksDto);
            }

            if (taskInfo.TaskId == InviteMilestoneTaskId)
            {
                var tasksDto = new TasksDto();

                _logger.LogDebug("check invite milestone for address:{address}", address);
                var cnt =
                    await _tasksProvider.GetInviteCountAsync(new List<string> { address }, MilestoneStartFrom);

                var lastClaimLevel = await GetMilestoneTaskLevelAsync(address, taskInfo.TaskId);
                var nextLevel = GetNextMilestoneLevel(lastClaimLevel, taskInfo);

                if (cnt >= nextLevel)
                {
                    tasksDto.Status = UserTaskStatus.Finished;
                }
                else
                {
                    tasksDto.Status = UserTaskStatus.Created;
                }

                tasksDto.Name = "invite friends (" + cnt + "/" + nextLevel + ")";

                taskList.Add(tasksDto);
            }
        }

        return taskList;
    }

    private int GetNextMilestoneLevel(int currentLevel, TaskConfig config)
    {
        var milestoneConfig = config.Milestone.Split(",");
        var beginLevels = milestoneConfig[0].Split("/");
        var step = int.Parse(milestoneConfig[1]);
        var lastConfigLevel = int.Parse(beginLevels.Last());
        if (currentLevel < lastConfigLevel)
        {
            foreach (var level in beginLevels)
            {
                if (currentLevel < int.Parse(level))
                {
                    return int.Parse(level);
                }
            }

            return lastConfigLevel;
        }
        else
        {
            var level = lastConfigLevel;
            while (level <= currentLevel)
            {
                level += step;
            }

            return level;
        }
    }

    public async Task<TaskData> FinishAsync(FinishInput input)
    {
        var currentAddress = await _userActionProvider.GetCurrentUserAddressAsync();
        // var currentAddress = input.Address;
        if (currentAddress.IsNullOrEmpty())
        {
            _logger.LogError("Get current address failed");
            throw new UserFriendlyException("Invalid user");
        }

        if (input.TaskId.IsNullOrEmpty())
        {
            _logger.LogError("empty taskId");
            throw new UserFriendlyException("empty taskId");
        }

        _logger.LogInformation("finish task, {taskId}, {address}", input.TaskId, currentAddress);

        var key = input.TaskId + "_" + currentAddress;
        var today = DateTime.UtcNow.ToString(TimeHelper.Pattern);
        // var today = "20241017";

        var date = "";
        var taskOption = _tasksOptions.CurrentValue;
        var taskList = taskOption.TaskList;
        var dailyTasks = taskList.Where(i => i.Type == TaskType.Daily).ToList();
        var dailyTaskId = dailyTasks.Select(i => i.TaskId).ToList();
        if (dailyTaskId.Contains(input.TaskId))
        {
            date = today;
            key += "_" + today;
        }

        await using var handle =
            await _distributedLock.TryAcquireAsync(key);

        if (handle == null)
        {
            _logger.LogError("get lock failed");
            throw new UserFriendlyException("please try later");
        }

        if (input.TaskId == InviteTaskId)
        {
            _logger.LogDebug("check invite for address:{address}", currentAddress);
            var inviterRecordsToday = await _tasksProvider.GetInviteRecordsToday(new List<string> { currentAddress });
            if (inviterRecordsToday.IsNullOrEmpty())
            {
                _logger.LogError("try finish task, but not invite, address: {address}", currentAddress);
                return new TaskData
                {
                    TaskId = input.TaskId,
                    Status = UserTaskStatus.Created
                };
            }
        }

        var nowUtc = DateTime.UtcNow;
        var todayBegin = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc);
        var todayEnd = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 23, 59, 59, DateTimeKind.Utc);

        if (input.TaskId == TradeTaskId)
        {
            var chainId = _levelOptions.CurrentValue.ChainIdForReal;
            var fullAddress = FullAddressHelper.ToFullAddress(currentAddress, chainId);
            var tradeRecordsToday = await _messageProvider.GetSchrodingerSoldListAsync(
                new GetSchrodingerSoldListInput
                {
                    Buyer = fullAddress,
                    FilterSymbol = "SGR",
                    MaxResultCount = 10,
                    SkipCount = 0,
                    Address = "",
                    TimestampMin = todayBegin.ToUtcMilliSeconds()
                });
            if (tradeRecordsToday.TotalRecordCount == 0)
            {
                _logger.LogError("try finish task, but not trade, address: {address}", currentAddress);
                return new TaskData
                {
                    TaskId = input.TaskId,
                    Status = UserTaskStatus.Created
                };
            }
        }

        if (input.TaskId == AdoptTaskId)
        {
            var resOfAdoption =
                await _adoptGraphQlProvider.GetAdoptInfoByTime(todayBegin.ToUtcSeconds(), todayEnd.ToUtcSeconds());
            var gen9AdoptByCurrentAddress =
                resOfAdoption.Where(i => i.Adopter == currentAddress && i.Gen == 9).ToList();
            _logger.LogDebug("check adopt for address:{address}, adopt times: {cnt}", currentAddress,
                gen9AdoptByCurrentAddress.Count);
            if (gen9AdoptByCurrentAddress.Count < 3)
            {
                _logger.LogError("try finish task, but adoption not enough, address: {address}, adopt times: {cnt}",
                    currentAddress, gen9AdoptByCurrentAddress.Count);
                // throw new UserFriendlyException("adoption not enough");
                return new TaskData
                {
                    TaskId = input.TaskId,
                    Status = UserTaskStatus.Created
                };
            }
        }

        if (input.TaskId == AdoptOnceTaskId)
        {
            var resOfAdoption =
                await _adoptGraphQlProvider.GetAdoptInfoByTime(todayBegin.ToUtcSeconds(), todayEnd.ToUtcSeconds());
            var gen9AdoptByCurrentAddress =
                resOfAdoption.Where(i => i.Adopter == currentAddress && i.Gen == 9).ToList();
            _logger.LogDebug("check adopt for address:{address}, adopt times: {cnt}", currentAddress,
                gen9AdoptByCurrentAddress.Count);
            if (gen9AdoptByCurrentAddress.Count < 1)
            {
                _logger.LogError("try finish task, but adoption not enough, address: {address}, adopt times: {cnt}",
                    currentAddress, gen9AdoptByCurrentAddress.Count);
                return new TaskData
                {
                    TaskId = input.TaskId,
                    Status = UserTaskStatus.Created
                };
            }
        }


        var res = await _tasksProvider.ChangeTaskStatusAsync(new ChangeTaskStatusInput
        {
            Address = currentAddress,
            TaskId = input.TaskId,
            Status = UserTaskStatus.Finished,
            Date = date
        });

        if (res == null)
        {
            _logger.LogError("user task not exist, address: {address}, task:{task}", currentAddress, input.TaskId);
            throw new UserFriendlyException("user task not exist");
        }

        if (res.Status != UserTaskStatus.Finished)
        {
            _logger.LogError("finish task failed, address: {address}, task:{task}, status:{status}", currentAddress,
                input.TaskId, res.Status);
            throw new UserFriendlyException("finish failed");
        }

        return _objectMapper.Map<TasksDto, TaskData>(res);
    }

    public async Task<ClaimOutput> ClaimAsync(ClaimInput input)
    {
        var currentAddress = await _userActionProvider.GetCurrentUserAddressAsync();
        // var currentAddress = input.Address;
        if (currentAddress.IsNullOrEmpty())
        {
            _logger.LogError("Get current address failed");
            throw new UserFriendlyException("Invalid user");
        }

        if (input.TaskId.IsNullOrEmpty())
        {
            _logger.LogError("empty taskId");
            throw new UserFriendlyException("empty taskId");
        }

        _logger.LogInformation("claim task, {taskId}, {address}", input.TaskId, currentAddress);

        var taskOption = _tasksOptions.CurrentValue;
        var taskList = taskOption.TaskList;

        var accomplishmentIds = taskList.Where(i => i.Type == TaskType.Accomplishment).Select(i => i.TaskId).ToList();

        if (accomplishmentIds.Contains(input.TaskId))
        {
            return await ClaimForAccomplishmentTaskAsync(input);
        }

        return await ClaimForNormalTaskAsync(input);
    }

    private async Task<ClaimOutput> ClaimForNormalTaskAsync(ClaimInput input)
    {
        var currentAddress = await _userActionProvider.GetCurrentUserAddressAsync();
        if (currentAddress.IsNullOrEmpty())
        {
            _logger.LogError("Get current address failed");
            throw new UserFriendlyException("Invalid user");
        }

        var today = DateTime.UtcNow.ToString(TimeHelper.Pattern);

        var date = "";
        var key = input.TaskId + "_" + currentAddress;

        var taskOption = _tasksOptions.CurrentValue;
        var taskList = taskOption.TaskList;

        var dailyTaskId = taskList.Where(i => i.Type == TaskType.Daily).Select(i => i.TaskId).ToList();

        if (dailyTaskId.Contains(input.TaskId))
        {
            date = today;
            key += "_" + date;
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
            TaskIdList = new List<string> { input.TaskId },
            Date = date
        });

        var taskInfo = tasks.FirstOrDefault();
        if (taskInfo == null)
        {
            _logger.LogError("user task not exist, address: {address}, task:{task}", currentAddress, input.TaskId);
            throw new UserFriendlyException("user task not exist");
        }

        if (taskInfo.Status == UserTaskStatus.Claimed)
        {
            _logger.LogError("already claimed, address:{address}, status:{status}, task:{task}", currentAddress,
                taskInfo.Status, input.TaskId);
            throw new UserFriendlyException("already claimed");
        }

        if (taskInfo.Status != UserTaskStatus.Finished)
        {
            _logger.LogError("invalid status, address:{address}, status:{status}, task:{task}", currentAddress,
                taskInfo.Status, input.TaskId);
            throw new UserFriendlyException("task not finished");
        }

        var res = await _tasksProvider.ChangeTaskStatusAsync(new ChangeTaskStatusInput
        {
            Address = currentAddress,
            TaskId = input.TaskId,
            Status = UserTaskStatus.Claimed,
            Date = date
        });

        if (res == null)
        {
            _logger.LogError("user task not exist, address: {address}, task:{task}", currentAddress, input.TaskId);
            throw new UserFriendlyException("user task not exist");
        }

        if (res.Status != UserTaskStatus.Claimed)
        {
            _logger.LogError("claim task failed, address: {address}, task:{task}, status:{status}", currentAddress,
                input.TaskId, res.Status);
            throw new UserFriendlyException("claim failed");
        }

        var score = res.Score;
        if (score == 0)
        {
            _logger.LogError("invalid taskId, address: {address}, task:{task}", currentAddress, input.TaskId);
            throw new UserFriendlyException("invalid taskId");
        }

        var totalScoreBefore = await GetCurrentFishScoreAsync(currentAddress);

        await _tasksProvider.AddTaskScoreDetailAsync(new AddTaskScoreDetailInput
        {
            Address = currentAddress,
            TaskId = input.TaskId,
            Score = score,
            Id = key
        });

        await Task.Delay(500);

        var output = _objectMapper.Map<TasksDto, ClaimOutput>(res);
        output.FishScore = totalScoreBefore + score;
        return output;
    }


    private async Task<ClaimOutput> ClaimForAccomplishmentTaskAsync(ClaimInput input)
    {
        var currentAddress = await _userActionProvider.GetCurrentUserAddressAsync();
        if (currentAddress.IsNullOrEmpty())
        {
            _logger.LogError("Get current address failed");
            throw new UserFriendlyException("Invalid user");
        }

        var key = input.TaskId + "_" + currentAddress;
        await using var handle =
            await _distributedLock.TryAcquireAsync(key);

        if (handle == null)
        {
            _logger.LogError("get lock failed");
            throw new UserFriendlyException("please try later");
        }

        var output = new ClaimOutput
        {
            TaskId = input.TaskId,
            FishScore = await GetCurrentFishScoreAsync(currentAddress)
        };

        var taskList = _tasksOptions.CurrentValue.TaskList;
        var taskInfo = taskList.First(i => i.TaskId == input.TaskId);

        if (input.TaskId == InviteMilestoneTaskId)
        {
            var cnt =
                await _tasksProvider.GetInviteCountAsync(new List<string> { currentAddress }, MilestoneStartFrom);

            var lastClaimLevel = await GetMilestoneTaskLevelAsync(currentAddress, input.TaskId);
            var nextLevel = GetNextMilestoneLevel(lastClaimLevel, taskInfo);

            if (cnt >= nextLevel)
            {
                // todo: call contract method to add voucher
                await SetMilestoneTaskLevelAsync(currentAddress, input.TaskId, nextLevel);
                output.Status = UserTaskStatus.Claimed;
            }
            else
            {
                _logger.LogError("invite not enough for next level, current: {cnt}, next: {nxt}", cnt, nextLevel);
                throw new UserFriendlyException("Invalid user");
            }
        }

        if (input.TaskId == AdoptMilestoneTaskId)
        {
            var res = await _adoptGraphQlProvider.GetAdoptInfoByTime(MilestoneStartFrom,
                TimeHelper.GetTimeStampInSeconds());
            var gen9AdoptByCurrentAddress =
                res.Where(i => i.Adopter == currentAddress && i.Gen == 9).ToList();
            var cnt = gen9AdoptByCurrentAddress.Count;

            var lastClaimLevel = await GetMilestoneTaskLevelAsync(currentAddress, input.TaskId);
            var nextLevel = GetNextMilestoneLevel(lastClaimLevel, taskInfo);

            if (cnt >= nextLevel)
            {
                // todo: call contract method to add voucher
                await SetMilestoneTaskLevelAsync(currentAddress, input.TaskId, nextLevel);
                output.Status = UserTaskStatus.Claimed;
            }
            else
            {
                _logger.LogError("adopt not enough for next level, current: {cnt}, next: {nxt}", cnt, nextLevel);
                throw new UserFriendlyException("Invalid user");
            }
        }

        return output;
    }

    public async Task<GetScoreOutput> GetScoreAsync(GetScoreInput input)
    {
        if (input.Address.IsNullOrEmpty())
        {
            _logger.LogError("empty address");
            throw new UserFriendlyException("empty address");
        }

        var res = await GetCurrentFishScoreAsync(input.Address);
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

    public async Task<GetTaskListOutput> GetTaskStatusAsync(GetTaskListInput input)
    {
        var tasksOptions = _tasksOptions.CurrentValue;
        var taskList = tasksOptions.TaskList;
        var dailyTasks = taskList.Where(i => i.Type == TaskType.Daily).ToList();
        var socialTasks = taskList.Where(i => i.Type == TaskType.Social).ToList();
        var accomplishmentTasks = taskList.Where(i => i.Type == TaskType.Accomplishment).ToList();

        var bizDate = DateTime.UtcNow.ToString(TimeHelper.Pattern);

        var dailyTaskList = await _tasksProvider.GetTasksAsync(new GetTasksInput
        {
            Address = input.Address,
            Date = bizDate,
            TaskIdList = dailyTasks.Select(i => i.TaskId).ToList()
        });
        dailyTaskList = dailyTaskList.OrderBy(i => GetSortOrder(i.Status)).ThenBy(i => i.Name).ToList();

        var socialTaskList = await _tasksProvider.GetTasksAsync(new GetTasksInput
        {
            Address = input.Address,
            TaskIdList = socialTasks.Select(i => i.TaskId).ToList()
        });
        socialTaskList = socialTaskList.OrderBy(i => GetSortOrder(i.Status)).ThenBy(i => i.Name).ToList();

        var accomplishmentTaskList = await _tasksProvider.GetTasksAsync(new GetTasksInput
        {
            Address = input.Address,
            TaskIdList = accomplishmentTasks.Select(i => i.TaskId).ToList()
        });
        accomplishmentTaskList =
            accomplishmentTaskList.OrderBy(i => GetSortOrder(i.Status)).ThenBy(i => i.Name).ToList();

        return new GetTaskListOutput
        {
            DailyTasks = _objectMapper.Map<List<TasksDto>, List<TaskData>>(dailyTaskList),
            SocialTasks = _objectMapper.Map<List<TasksDto>, List<TaskData>>(socialTaskList),
            AccomplishmentTasks = _objectMapper.Map<List<TasksDto>, List<TaskData>>(accomplishmentTaskList)
        };
    }

    public async Task<SpinOutput> SpinAsync()
    {
        _logger.LogDebug("SpinAsync");

        var currentAddress = await _userActionProvider.GetCurrentUserAddressAsync();
        if (currentAddress.IsNullOrEmpty())
        {
            _logger.LogError("Get current address failed");
            throw new UserFriendlyException("Invalid user");
        }

        var key = "spin" + "_" + currentAddress;
        await using var handle =
            await _distributedLock.TryAcquireAsync(key);

        _logger.LogDebug("SpinAsync for address, address:{address}", currentAddress);

        // check if score is enough
        var currentScore = await GetCurrentFishScoreAsync(currentAddress);
        if (currentScore < 100)
        {
            _logger.LogError("not enough fish score, address:{address}, score:{score}", currentAddress, currentScore);
            throw new UserFriendlyException("not enough score");
        }

        // query last spin seed from cache
        var cache = await _spinCache.GetAsync(key);
        var nowTs = TimeHelper.GetTimeStampInSeconds();
        if (cache != null)
        {
            _logger.LogWarning(
                "found cache, seed: {id}", cache.Seed);
            var isSpinFinished = await _tasksProvider.IsSpinFinished(cache.Seed);

            if (!isSpinFinished && nowTs < cache.ExpirationTime)
            {
                _logger.LogWarning(
                    "found unfinished seed in cache, seed: {id}, sig: {sig} ", cache.Seed, cache.Signature);

                return new SpinOutput
                {
                    Seed = cache.Seed,
                    Tick = DefaultTick,
                    Signature = ByteStringHelper.FromHexString(cache.Signature),
                    ExpirationTime = cache.ExpirationTime
                };
            }
        }

        // query unfinished spin seed from es
        var unfinishedSpin = await _tasksProvider.GetUnfinishedSpinAsync(currentAddress);
        var validSpins = unfinishedSpin.Where(i => i.ExpirationTime > nowTs).ToList();
        if (!validSpins.IsNullOrEmpty())
        {
            var validSpin = validSpins.FirstOrDefault();
            _logger.LogWarning(
                "already generated signature, seed: {id}", validSpin.Seed);

            var isSpinFinished = await _tasksProvider.IsSpinFinished(validSpin.Seed);
            if (isSpinFinished)
            {
                await _tasksProvider.FinishSpinAsync(validSpin.Seed);
                _logger.LogWarning(
                    "finish spin, seed: {id}", validSpin.Seed);
            }
            else
            {
                return new SpinOutput
                {
                    Seed = validSpin.Seed,
                    Tick = DefaultTick,
                    Signature = ByteStringHelper.FromHexString(validSpin.Signature),
                    ExpirationTime = validSpin.ExpirationTime
                };
            }
        }

        // generate new spin seed and signature
        var now = DateTime.UtcNow;
        var expirationTime = new DateTime(now.Year, now.Month, now.Day)
            .AddYears(1)
            .ToUtcSeconds();
        var uid = Guid.NewGuid().ToString();
        var seed = HashHelper.ComputeFrom(uid);

        var data = new SpinInput
        {
            Tick = DefaultTick,
            Seed = seed,
            ExpirationTime = expirationTime
        };
        var dataHash = HashHelper.ComputeFrom(data);
        var signature = await _secretProvider.GetSignatureFromHashAsync(_chainOptions.PublicKey, dataHash);

        // write new cache
        var cacheData = new SpinOutputCache
        {
            Seed = seed.ToHex(),
            Tick = DefaultTick,
            ExpirationTime = expirationTime,
            Signature = signature
        };
        await _spinCache.SetAsync(key, cacheData, new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromDays(1)
        });

        // write es
        await _tasksProvider.AddSpinAsync(new AddSpinInput
        {
            Address = currentAddress,
            Seed = seed.ToHex(),
            ExpirationTime = expirationTime,
            Tick = DefaultTick,
            Signature = signature
        });

        // wait for es to be read
        await Task.Delay(200);

        _logger.LogWarning(
            "generate new seed: {id}, sig: {sig} ", seed.ToHex(), signature);

        return new SpinOutput
        {
            Seed = seed.ToHex(),
            Tick = DefaultTick,
            ExpirationTime = expirationTime,
            Signature = ByteStringHelper.FromHexString(signature)
        };
    }

    private async Task<decimal> GetCurrentFishScoreAsync(string address)
    {
        var scoreFromTask = await _tasksProvider.GetTotalScoreFromTask(address);
        _logger.LogDebug("Get fish score from task, address:{address}, score:{score}", address, scoreFromTask);

        // var scoreConsumeFromSpin = await _tasksProvider.GetConsumeScoreFromSpin(address);
        var scoreConsumeFromSpin = 0;
        var unfinishedSpin = await _tasksProvider.GetUnfinishedSpinAsync(address);
        foreach (var spin in unfinishedSpin)
        {
            var isSpinFinished = await _tasksProvider.IsSpinFinished(spin.Seed);
            if (isSpinFinished)
            {
                scoreConsumeFromSpin += 100;
            }
        }

        var finsihedSpinCount = await _tasksProvider.GetFinishedSpinCountAsync(address);
        scoreConsumeFromSpin += finsihedSpinCount * 100;
        _logger.LogDebug("Get consume fish score from spin, address:{address}, score:{score}", address,
            scoreConsumeFromSpin);

        var scoreFromSpinReward = await _tasksProvider.GetScoreFromSpinRewardAsync(address);
        _logger.LogDebug("Get spin reward fish score, address:{address}, score:{score}", address, scoreFromSpinReward);

        return scoreFromTask + scoreFromSpinReward - scoreConsumeFromSpin;
    }


    public async Task<VoucherAdoptionOutput> VoucherAdoptionAsync(VoucherAdoptionInput input)
    {
        var currentAddress = await _userActionProvider.GetCurrentUserAddressAsync();
        _logger.LogDebug("VoucherAdoptionAsync, id: {id}, address: {address}", input.VoucherId, currentAddress);

        var voucherAdoptionResult = await _tasksProvider.GetVoucherAdoptionAsync(input.VoucherId);
        if (voucherAdoptionResult == null || voucherAdoptionResult.VoucherId.IsNullOrEmpty())
        {
            _logger.LogError("Get voucher adoption failed, id: {id}", input.VoucherId);
            return new VoucherAdoptionOutput();
        }

        var isRare = !voucherAdoptionResult.Rarity.IsNullOrEmpty();
        if (!isRare)
        {
            _logger.LogInformation("Voucher is not rare, id: {id}", input.VoucherId);
            return new VoucherAdoptionOutput
            {
                VoucherId = input.VoucherId,
                IsRare = false
            };
        }

        var data = new ConfirmVoucherInput
        {
            VoucherId = Hash.LoadFromHex(input.VoucherId)
        };
        var dataHash = HashHelper.ComputeFrom(data);
        var signature = await _secretProvider.GetSignatureFromHashAsync(_chainOptions.PublicKey, dataHash);
        return new VoucherAdoptionOutput
        {
            VoucherId = input.VoucherId,
            Signature = ByteStringHelper.FromHexString(signature),
            IsRare = true
        };
    }


    public async Task<SpinRewardOutput> SpinRewardAsync()
    {
        _logger.LogError("Get rewardConfig");
        var rewardOptions = _spinRewardOptions.CurrentValue;
        if (rewardOptions == null || rewardOptions.RewardList.IsNullOrEmpty())
        {
            _logger.LogError("Get rewardOption failed");
            throw new UserFriendlyException("Get rewardOption failed");
        }

        var rewardConfig = await _tasksProvider.GetSpinRewardConfigAsync();
        if (rewardConfig == null || rewardConfig.RewardList.IsNullOrEmpty())
        {
            _logger.LogError("Get rewardConfig failed");
            throw new UserFriendlyException("Get rewardConfig failed");
        }

        var list = new List<RewardItem>();

        foreach (var reward in rewardConfig.RewardList)
        {
            list.Add(new RewardItem
            {
                Name = reward.Name,
                Content = rewardOptions.RewardList.FirstOrDefault(i => i.Name == reward.Name)?.Content ?? ""
            });
        }

        return new SpinRewardOutput
        {
            RewardList = list
        };
    }

    public async Task LogTgBotAsync(LogTgBotInput input)
    {
        _logger.LogDebug("LogTgBotAsync");

        var currentAddress = await _userActionProvider.GetCurrentUserAddressAsync();
        if (currentAddress.IsNullOrEmpty())
        {
            _logger.LogError("LogTgBotAsync, Get current address failed");
            throw new UserFriendlyException("Invalid user");
        }

        var currentScore = await GetCurrentFishScoreAsync(currentAddress);

        input.Address = currentAddress;
        input.Score = currentScore;
        await _tasksProvider.AddTgBotLogAsync(input);
    }

    private async Task<int> GetMilestoneTaskLevelAsync(string address, string taskId)
    {
        var key = $"milestone_{address}_{taskId}";
        var cache = await _milestoneTaskCache.GetAsync(key);
        if (cache != null)
        {
            return cache.Level;
        }

        return await _tasksProvider.GetLastMilestoneLevelAsync(address, taskId);
    }

    private async Task SetMilestoneTaskLevelAsync(string address, string taskId, int level)
    {
        var key = $"milestone_{address}_{taskId}";

        var cacheData = new MilestoneTaskCache
        {
            TaskId = taskId,
            Level = level
        };
        await _milestoneTaskCache.SetAsync(key, cacheData, new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromDays(1)
        });

        var index = new MilestoneVoucherClaimedIndex
        {
            Id = $"{address}_{taskId}_{level}",
            Address = address,
            TaskId = taskId,
            Level = level,
            Amount = 1,
            CreatedTime = TimeHelper.GetTimeStampInSeconds()
        };
        await _tasksProvider.AddMilestoneVoucherClaimedAsync(index);
    }

    public async Task SendAirdropVoucherTransactionAsync(string chainId, string address)
    {
        var aelfAddress = Address.FromBase58(address);
        var param = new AirdropVoucherInput
        {
            Tick = DefaultTick,
            Amount = 1,
            List = { aelfAddress }
        };

        var rawTxResult = await _contractProvider.CreateTransactionAsync(chainId,
            _chainOptions.ChainInfos[chainId].PointTxPublicKey,
            _chainOptions.ChainInfos[chainId].SchrodingerContractAddress, "AirdropVoucher", param);
        _logger.LogInformation("SendAirdropVoucherTransactionAsync rawTxResult: {result}", JsonConvert.SerializeObject(rawTxResult));

        var signedTransaction = rawTxResult.transaction;

        var transactionOutput = await _contractProvider.SendTransactionWithRetAsync(chainId, signedTransaction);
        _logger.LogInformation("SendAirdropVoucherTransactionAsync transactionId: {id}", transactionOutput.TransactionId);
        var transactionResult =
            await _contractProvider.CheckTransactionStatusAsync(transactionOutput.TransactionId, chainId);
        
        if (!transactionResult.Result)
        {
            throw new UserFriendlyException(transactionResult.Error);
        }
    }
}