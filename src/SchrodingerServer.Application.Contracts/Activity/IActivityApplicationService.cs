using System.Collections.Generic;
using System.Threading.Tasks;
using SchrodingerServer.AddressRelationship.Dto;
using SchrodingerServer.Users.Dto;

namespace SchrodingerServer.Activity;

public interface IActivityApplicationService
{
    Task<List<ActivityDto>> GetActivityListAsync(GetActivityListInput input);
    
    Task<ActivityInfoDto> GetActivityInfoAsync();
    
    Task BindActivityAddressAsync(BindActivityAddressInput input);
    
    Task<ActivityAddressDto> GetActivityAddressAsync(GetActivityAddressInput input);
}