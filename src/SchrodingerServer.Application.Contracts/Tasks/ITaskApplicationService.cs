using System.Threading.Tasks;
using SchrodingerServer.Common.Dtos;
using SchrodingerServer.Tasks.Dtos;

namespace SchrodingerServer.Tasks;

public interface ITasksApplicationService
{
    Task<GetTaskListOutput> GetTaskListAsync(GetTaskListInput input);
    Task<TaskData> FinishAsync(FinishInput input);
    Task<ClaimOutput> ClaimAsync(ClaimInput input);
    Task<GetScoreOutput> GetScoreAsync(GetScoreInput input);
    Task<GetTaskListOutput> GetTaskStatusAsync(GetTaskListInput input);
    Task<SpinOutput> SpinAsync();
    Task<VoucherAdoptionOutput> VoucherAdoptionAsync(VoucherAdoptionInput input);
    Task<SpinRewardOutput> SpinRewardAsync();
    Task LogTgBotAsync(LogTgBotInput input);
    Task<CheckTransactionDto> SendAirdropVoucherTransactionAsync(string address, string chainId);
    Task<bool> CheckUserAsync(string userId);
}