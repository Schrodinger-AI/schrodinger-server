using System;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SchrodingerServer.Users.Eto;
using SchrodingerServer.Users.Index;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.EntityEventHandler.Core.IndexHandler;

public class UserInformationHandler : IDistributedEventHandler<UserInformationEto>,
    ITransientDependency
{
    private readonly INESTRepository<UserIndex, Guid> _userRepository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<UserInformationHandler> _logger;

    public UserInformationHandler(INESTRepository<UserIndex, Guid> userRepository, IObjectMapper objectMapper, ILogger<UserInformationHandler> logger)
    {
        _userRepository = userRepository;
        _objectMapper = objectMapper;
        _logger = logger;
    }


    public async Task HandleEventAsync(UserInformationEto eventData)
    {
        try
        {
            var contact = _objectMapper.Map<UserInformationEto, UserIndex>(eventData);
            await _userRepository.AddOrUpdateAsync(contact);
            _logger.LogDebug("HandleEventAsync UserInformationEto success");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", JsonConvert.SerializeObject(eventData));
        }
        
    }
    
    
    
}