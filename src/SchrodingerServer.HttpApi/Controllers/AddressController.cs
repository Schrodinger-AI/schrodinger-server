using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchrodingerServer.AddressRelationship;
using SchrodingerServer.AddressRelationship.Dto;
using SchrodingerServer.Users.Dto;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace SchrodingerServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Address")]
[Route("api/app")]
public class AddressController : AbpController
{
    private readonly IAddressRelationshipApplicationService _addressRelationshipApplicationService;
    
    public AddressController(IAddressRelationshipApplicationService addressRelationshipApplicationService)
    {
        _addressRelationshipApplicationService = addressRelationshipApplicationService;
    }
    
    
    [HttpPost("bind-address")]
    [Authorize]
    public Task BindAddressAsync(BindAddressInput input)
    {
        return  _addressRelationshipApplicationService.BindAddressAsync(input);
    }
    
    
    [HttpGet("remain-point")]
    public Task<RemainPointDto> GetRemainPointAsync()
    {
        return _addressRelationshipApplicationService.GetRemainPointAsync();
    }
}