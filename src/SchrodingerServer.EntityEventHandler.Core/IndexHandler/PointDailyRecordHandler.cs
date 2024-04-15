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

public class PointDailyRecordHandler : IDistributedEventHandler<PointDailyRecordEto>, ITransientDependency
{
    private readonly INESTRepository<PointDailyRecordIndex, string> _repository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<ContractInvokeHandler> _logger;

    public PointDailyRecordHandler(INESTRepository<PointDailyRecordIndex, string> repository, 
        IObjectMapper objectMapper, 
        ILogger<ContractInvokeHandler> logger)
    {
        _repository = repository;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task HandleEventAsync(PointDailyRecordEto eventData)
    {
        try
        {
            var contact = _objectMapper.Map<PointDailyRecordEto, PointDailyRecordIndex>(eventData);
            await _repository.AddOrUpdateAsync(contact);
            _logger.LogDebug("HandleEventAsync PointDailyRecordEto success");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", JsonConvert.SerializeObject(eventData));
        }
    }
}