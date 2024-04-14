using System;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SchrodingerServer.ContractInvoke.Eto;
using SchrodingerServer.ContractInvoke.Index;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.EntityEventHandler.Core.IndexHandler;

public class ContractInvokeHandler : IDistributedEventHandler<ContractInvokeEto>, ITransientDependency
{
    private readonly INESTRepository<ContractInvokeIndex, string> _repository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<ContractInvokeHandler> _logger;

    public ContractInvokeHandler(INESTRepository<ContractInvokeIndex, string> repository, 
        IObjectMapper objectMapper, 
        ILogger<ContractInvokeHandler> logger)
    {
        _repository = repository;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task HandleEventAsync(ContractInvokeEto eventData)
    {
        try
        {
            var contact = _objectMapper.Map<ContractInvokeEto, ContractInvokeIndex>(eventData);
            await _repository.AddOrUpdateAsync(contact);
            _logger.LogDebug("HandleEventAsync ContractInvokeEto success");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", JsonConvert.SerializeObject(eventData));
        }
    }
}