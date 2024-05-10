using System.Threading.Tasks;
using SchrodingerServer.Message.Dtos;

namespace SchrodingerServer.Message;

public interface IMessageApplicationService
{
    Task<long> GetUnreadCountAsync(GetUnreadMessageCountInput input);

    Task<MessageListDto> GetMessageListAsync(GetMessageListInput input);
}