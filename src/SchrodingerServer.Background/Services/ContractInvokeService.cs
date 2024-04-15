using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using Microsoft.Extensions.Logging;
using Nest;
using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.ContractInvoke.Eto;
using SchrodingerServer.ContractInvoke.Index;
using SchrodingerServer.Grains.Grain.ContractInvoke;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Background.Services;

public interface IContractInvokeService
{
    Task<List<string>> SearchUnfinishedTransactionsAsync(int limit);

    Task ExecuteJobAsync(string bizId);
}

public class ContractInvokeService : IContractInvokeService, ISingletonDependency
{
    private readonly INESTRepository<ContractInvokeIndex, string> _contractInvokeIndexRepository;
    private readonly ILogger<ContractInvokeService> _logger;
    private readonly IClusterClient _clusterClient;
    private readonly IObjectMapper _objectMapper;
    private readonly IDistributedEventBus _distributedEventBus;

    public ContractInvokeService(
        INESTRepository<ContractInvokeIndex, string> contractInvokeIndexRepository,
        ILogger<ContractInvokeService> logger, IClusterClient clusterClient, IObjectMapper objectMapper,
        IDistributedEventBus distributedEventBus)
    {
        _contractInvokeIndexRepository = contractInvokeIndexRepository;
        _logger = logger;
        _clusterClient = clusterClient;
        _objectMapper = objectMapper;
        _distributedEventBus = distributedEventBus;
    }

    public async Task<List<string>> SearchUnfinishedTransactionsAsync(int limit)
    {
        var mustNotQuery = new List<Func<QueryContainerDescriptor<ContractInvokeIndex>, QueryContainer>>()
        {
            q => q.Match(m 
                => m.Field(f => f.Status).Query(ContractInvokeStatus.Success.ToString())),
            q => q.Match(m 
                => m.Field(f => f.Status).Query(ContractInvokeStatus.FinalFailed.ToString()))

        };

        QueryContainer Filter(QueryContainerDescriptor<ContractInvokeIndex> f) =>
            f.Bool(b => b.MustNot(mustNotQuery));

        var (_, synchronizeTransactions) = await _contractInvokeIndexRepository
            .GetListAsync(Filter, limit: limit);

        var newList = synchronizeTransactions.Where(x => !x.BizId.IsNullOrEmpty()).ToList();

        _logger.LogInformation(
            "There are {COUNT} transactions that have not completed the contract invoke transaction", newList.Count);

        return newList.Count < 1 ? new List<string>() : newList.Select(o => o.BizId).ToList();
    }

    public async Task ExecuteJobAsync(string bizId)
    {
        var syncTxEsData = await SearchContractInvokeTxByIdAsync(bizId);

        var contractInvokeGrain = _clusterClient.GetGrain<IContractInvokeGrain>(bizId);
        
        var result = await contractInvokeGrain.ExecuteJobAsync(
            _objectMapper.Map<ContractInvokeEto, ContractInvokeGrainDto>(syncTxEsData));

        _logger.LogInformation(
            "Execute transaction job in grain successfully, ready to update {bizId} {status}", bizId,
            result.Data.Status);

        if (syncTxEsData.Status == result.Data.Status)
        {
            return;
        }

        var syncTxEtoData =  _objectMapper.Map<ContractInvokeGrainDto, ContractInvokeEto>(result.Data);

        await _distributedEventBus.PublishAsync(syncTxEtoData);
    }

    private async Task<ContractInvokeEto> SearchContractInvokeTxByIdAsync(string bizId)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<ContractInvokeIndex>, QueryContainer>>
        {
            q => q.Terms(i => i.Field(f => f.BizId).Terms(bizId))
        };

        QueryContainer Filter(QueryContainerDescriptor<ContractInvokeIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var (totalCount, syncTxs) = await _contractInvokeIndexRepository.GetListAsync(Filter);

        return totalCount < 1
            ? new ContractInvokeEto()
            : _objectMapper.Map<ContractInvokeIndex, ContractInvokeEto>(syncTxs.First());
    }
}