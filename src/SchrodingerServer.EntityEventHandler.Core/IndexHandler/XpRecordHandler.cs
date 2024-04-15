using System;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SchrodingerServer.Zealy;
using SchrodingerServer.Zealy.Eto;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.EntityEventHandler.Core.IndexHandler;

public class XpRecordHandler : IDistributedEventHandler<XpRecordEto>, IDistributedEventHandler<AddXpRecordEto>,
    ITransientDependency
{
    private readonly INESTRepository<ZealyUserXpRecordIndex, string> _repository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<XpRecordHandler> _logger;

    public XpRecordHandler(INESTRepository<ZealyUserXpRecordIndex, string> repository, IObjectMapper objectMapper,
        ILogger<XpRecordHandler> logger)
    {
        _repository = repository;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task HandleEventAsync(XpRecordEto eventData)
    {
        try
        {
            var contact = _objectMapper.Map<XpRecordEto, ZealyUserXpRecordIndex>(eventData);
            await _repository.AddOrUpdateAsync(contact);
            _logger.LogInformation("add or update xp record success, recordId:{recordId}", eventData.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "add or update xp record error, data:{data}", JsonConvert.SerializeObject(eventData));
        }
    }

    public async Task HandleEventAsync(AddXpRecordEto eventData)
    {
        try
        {
            var contact = _objectMapper.Map<AddXpRecordEto, ZealyUserXpRecordIndex>(eventData);
            await _repository.AddAsync(contact);
            _logger.LogInformation("add xp record success, recordId:{recordId}", eventData.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "add xp record error, data:{data}", JsonConvert.SerializeObject(eventData));
        }
    }
}