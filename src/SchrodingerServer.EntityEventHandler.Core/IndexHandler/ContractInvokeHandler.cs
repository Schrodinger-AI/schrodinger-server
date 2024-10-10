using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SchrodingerServer.ContractInvoke.Eto;
using SchrodingerServer.ContractInvoke.Index;
using SchrodingerServer.ExceptionHandling;
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
    
    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionNull))]
    public async Task HandleEventAsync(ContractInvokeEto eventData)
    {
        var contact = _objectMapper.Map<ContractInvokeEto, ContractInvokeIndex>(eventData);
        await _repository.AddOrUpdateAsync(contact);
        _logger.LogDebug("HandleEventAsync ContractInvokeEto success");
    }
}