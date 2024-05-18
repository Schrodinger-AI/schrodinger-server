using System.Threading.Tasks;
using SchrodingerServer.Users.Dto;

namespace SchrodingerServer.AddressRelationship;

public interface IAddressRelationshipApplicationService
{
    Task BindAddressAsync(BindAddressInput input);
}