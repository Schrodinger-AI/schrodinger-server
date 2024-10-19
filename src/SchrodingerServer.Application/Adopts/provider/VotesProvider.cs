using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Activity.Index;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Adopts.provider;

public interface IVotesProvider
{
    Task VoteAsync(string adoptId, int faction, string address);
    Task<long> VoteCountAsync(int faction);
    Task<VotesRecordIndex> GetVoteAsync(string adoptId);
}

public class VotesProvider : IVotesProvider, ISingletonDependency
{
    private readonly INESTRepository<VotesRecordIndex, string> _votesRecordIndexRepository;
    // private readonly ILogger<VotesProvider> _logger;
    // private readonly IObjectMapper _objectMapper;
    
    public VotesProvider(
        INESTRepository<VotesRecordIndex, string> votesRecordIndexRepository
        )
    {
        _votesRecordIndexRepository = votesRecordIndexRepository;
    }
    
    public async Task VoteAsync(string adoptId, int faction, string address)
    {
        var index = new VotesRecordIndex()
        {
            Id = adoptId,
            Address = address,
            Faction = faction,
            AdoptId = adoptId,
            CreatedTime = DateTime.UtcNow
        };
            
        await _votesRecordIndexRepository.AddAsync(index);
    }

    public async Task<long> VoteCountAsync(int faction)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<VotesRecordIndex>, QueryContainer>>
        {
            q => q.Term(i
                => i.Field(index => index.Faction).Value(faction))
        };
        
        QueryContainer Filter(QueryContainerDescriptor<VotesRecordIndex> f) => f.Bool(b => b.Must(mustQuery));
        var countResp = await _votesRecordIndexRepository.CountAsync(Filter);
        return countResp.Count;
    }
    
    public async Task<VotesRecordIndex> GetVoteAsync(string adoptId)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<VotesRecordIndex>, QueryContainer>>
        {
            q => q.Term(i
                => i.Field(index => index.AdoptId).Value(adoptId))
        };
        
        QueryContainer Filter(QueryContainerDescriptor<VotesRecordIndex> f) => f.Bool(b => b.Must(mustQuery));
        var index = await _votesRecordIndexRepository.GetAsync(Filter);
        return index;
    }
}