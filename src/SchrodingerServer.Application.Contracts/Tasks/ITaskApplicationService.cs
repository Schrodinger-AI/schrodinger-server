using System.Threading.Tasks;
using SchrodingerServer.Tasks.Dtos;

namespace SchrodingerServer.Tasks;

public interface ITasksApplicationService
{
    Task<GetTaskListOutput> GetTaskListAsync(GetTaskListInput input);
    Task<TaskData> FinishAsync(FinishInput input);
    Task<ClaimOutput> ClaimAsync(ClaimInput input);
    Task<GetScoreOutput> GetScoreAsync(GetScoreInput input);
    Task<GetTaskListOutput> GetTaskStatusAsync(GetTaskListInput input);
}