using System.Threading.Tasks;
using SchrodingerServer.AddressRelationship.Dto;
using SchrodingerServer.Users.Dto;

namespace SchrodingerServer.AddressRelationship;

public interface IAddressRelationshipApplicationService
{
    Task BindAddressAsync(BindAddressInput input);
    
    Task<RemainPointDto> GetRemainPointAsync();
}