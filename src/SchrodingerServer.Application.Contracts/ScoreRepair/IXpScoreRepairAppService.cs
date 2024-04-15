using System.Collections.Generic;
using System.Threading.Tasks;
using SchrodingerServer.ScoreRepair.Dtos;

namespace SchrodingerServer.ScoreRepair;

public interface IXpScoreRepairAppService
{
    Task UpdateScoreRepairDataAsync(List<UpdateXpScoreRepairDataDto> input);
    Task<XpScoreRepairDataPageDto> GetXpScoreRepairDataAsync(XpScoreRepairDataRequestDto input);
    Task<UserXpInfoDto> GetUserXpAsync(UserXpInfoRequestDto input);
    Task<XpRecordPageResultDto> GetUserRecordsAsync(string userId, int skipCount, int maxResultCount);
    Task ReCreateContractAsync(ReCreateDto input);
}