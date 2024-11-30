using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SchrodingerServer.ExceptionHandling;
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
    private readonly ILogger<PointDailyRecordHandler> _logger;

    public PointDailyRecordHandler(INESTRepository<PointDailyRecordIndex, string> repository, 
        IObjectMapper objectMapper, 
        ILogger<PointDailyRecordHandler> logger)
    {
        _repository = repository;
        _objectMapper = objectMapper;
        _logger = logger;
    }
    
    public async Task HandleEventAsync(PointDailyRecordEto eventData)
    {
        _logger.LogDebug("HandleEventAsync PointDailyRecordEto data: {data}", JsonConvert.SerializeObject(eventData));
        var contact = _objectMapper.Map<PointDailyRecordEto, PointDailyRecordIndex>(eventData);
        if (eventData.Id.IsNullOrEmpty())
        {
            _logger.LogDebug("HandleEventAsync Id empty, {data}", JsonConvert.SerializeObject(eventData));
            return;
        }
        await _repository.AddOrUpdateAsync(contact);
        _logger.LogDebug("HandleEventAsync PointDailyRecordEto success: {data}", JsonConvert.SerializeObject(eventData));
    }
}