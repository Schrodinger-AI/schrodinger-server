using System.Collections.Generic;
using System.Threading.Tasks;
using SchrodingerServer.Dto;

namespace SchrodingerServer.Users;

public interface ILevelProvider
{
    Task<List<RankData>> GetItemLevelAsync(GetLevelInfoInputDto input);

    Task<double> GetAwakenSGRPrice();

    Task<LevelInfoDto> GetItemLevelDicAsync(int rank, double price);

}