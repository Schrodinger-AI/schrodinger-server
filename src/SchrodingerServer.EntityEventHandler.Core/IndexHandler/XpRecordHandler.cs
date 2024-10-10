using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SchrodingerServer.ExceptionHandling;
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
    
    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionNull))]
    public async Task HandleEventAsync(XpRecordEto eventData)
    {
        var contact = _objectMapper.Map<XpRecordEto, ZealyUserXpRecordIndex>(eventData);
        await _repository.AddOrUpdateAsync(contact);
        _logger.LogInformation("add or update xp record success, recordId:{recordId}", eventData.Id);
    }
    
    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHandlingService), MethodName = nameof(ExceptionHandlingService.HandleExceptionDefault))]
    public async Task HandleEventAsync(AddXpRecordEto eventData)
    {
        var contact = _objectMapper.Map<AddXpRecordEto, ZealyUserXpRecordIndex>(eventData);
        await _repository.AddAsync(contact);
        _logger.LogInformation("add xp record success, recordId:{recordId}", eventData.Id);
    }
}