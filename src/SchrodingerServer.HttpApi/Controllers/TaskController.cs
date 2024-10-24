using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchrodingerServer.Tasks;
using SchrodingerServer.Tasks.Dtos;
using Volo.Abp;

namespace SchrodingerServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Task")]
[Route("api/app/task")]
public class TaskController
{
    private readonly ITasksApplicationService _tasksApplicationService;
    
    public TaskController(ITasksApplicationService tasksApplicationService )
    {
        _tasksApplicationService = tasksApplicationService;
    }
    
    [Authorize]
    [HttpPost("list")]
    public async Task<GetTaskListOutput> GetTaskListAsync(GetTaskListInput input)
    {
        return await _tasksApplicationService.GetTaskListAsync(input);
    }
    
    [Authorize]
    [HttpPost("finish")]
    public async Task<TaskData> FinishAsync(FinishInput input)
    {
        return await _tasksApplicationService.FinishAsync(input);
    }
    
    [Authorize]
    [HttpPost("claim")]
    public async Task<ClaimOutput> ClaimAsync(ClaimInput input)
    {
        return await _tasksApplicationService.ClaimAsync(input);
    }
    
    [HttpPost("score")]
    public async Task<GetScoreOutput> GetScoreAsync(GetScoreInput input)
    {
        return await _tasksApplicationService.GetScoreAsync(input);
    }
    
    [HttpPost("task-status")]
    public async Task<GetTaskListOutput> GetTaskStatusAsync(GetTaskListInput input)
    {
        return await _tasksApplicationService.GetTaskStatusAsync(input);
    }
    
}