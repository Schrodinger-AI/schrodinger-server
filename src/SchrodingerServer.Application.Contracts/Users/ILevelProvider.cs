using System.Collections.Generic;
using System.Threading.Tasks;
using SchrodingerServer.Dto;

namespace SchrodingerServer.Users;

public interface ILevelProvider
{
    Task<List<RankData>> GetItemLevelAsync(GetLevelInfoInputDto input);
    
    Task<List<RankData>> GetRankLevelAsync(GetLevelInfoInputDto input);

    Task<double> GetAwakenSGRPrice();

    Task<LevelInfoDto> GetItemLevelDicAsync(int rank, double price);

    Task<bool> CheckAddressIsInWhiteListAsync(string address);
    
    Task<List<NftInfo>> BatchGetForestNftInfoAsync(List<string> nftIdList, string chainId);
}