using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SchrodingerServer.Message;
using SchrodingerServer.Message.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace SchrodingerServer.Controllers;


[RemoteService]
[Area("app")]
[ControllerName("message")]
[Route("api/app/message")]
public class MessageController : AbpController
{
    private readonly IMessageApplicationService _messageApplicationService;
    
    public MessageController(IMessageApplicationService messageApplicationService)
    {
        _messageApplicationService = messageApplicationService;
    }
    
    [HttpPost("unread-count")]
    public async Task<long> GetSchrodingerCatList(GetUnreadMessageCountInput input)
    {
        return await _messageApplicationService.GetUnreadCountAsync(input);
    }
    
    [HttpPost("list")]
    public async Task<MessageListDto> GetMessageListAsync(GetMessageListInput input)
    {
        return await _messageApplicationService.GetMessageListAsync(input);
    }
}