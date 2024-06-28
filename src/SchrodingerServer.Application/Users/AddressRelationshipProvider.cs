using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using Nest;
using Orleans;
using SchrodingerServer.Common;
using SchrodingerServer.Users.Index;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Users;


public interface IAddressRelationshipProvider
{
    Task BindAddressAsync(string aelfAddress, string evmAddress);
    
    Task<bool> CheckBindingExistsAsync(string aelfAddress, string evmAddress);
    
    Task<string> GetAelfAddressByEvmAddressAsync(string evmAddress);
    
    Task BindActivityAddressAsync(string aelfAddress, string sourceChainAddress, ChainType chainType, string activityId);
    
    Task<ActivityAddressIndex> GetActivityAddressAsync(string aelfAddress, string activityId);
    Task<bool> CheckActivityBindingExistsAsync(string aelfAddress, string sourceChainAddress, string activityId);
    Task<string> GetEvmAddressByAelfAddressAsync(string alefAddress);
}


public class AddressRelationshipProvider : IAddressRelationshipProvider, ISingletonDependency
{
    private readonly INESTRepository<AddressRelationshipIndex, string> _addressRelationshipRepository;
    private readonly INESTRepository<ActivityAddressIndex, string> _activityAddressRepository;
    
    public AddressRelationshipProvider(
        INESTRepository<AddressRelationshipIndex, string> addressRelationshipRepository,
        INESTRepository<ActivityAddressIndex, string> activityAddressRepository)
    {
        _addressRelationshipRepository = addressRelationshipRepository;
        _activityAddressRepository = activityAddressRepository;
    }

    public async Task BindAddressAsync(string aelfAddress, string evmAddress)
    {
        var index = new AddressRelationshipIndex
        {
            Id = IdGenerateHelper.GetId(aelfAddress, evmAddress),
            AelfAddress = aelfAddress,
            EvmAddress = evmAddress,
            CreatedTime = DateTime.UtcNow
        };

        await _addressRelationshipRepository.AddAsync(index);
    }


    public async Task<bool> CheckBindingExistsAsync(string aelfAddress, string evmAddress)
    {
        if (aelfAddress.IsNullOrEmpty() && evmAddress.IsNullOrEmpty())
        {
            return false;
        }
        
        var shouldQuery = new List<Func<QueryContainerDescriptor<AddressRelationshipIndex>, QueryContainer>>();
        shouldQuery.Add(q => q.Term(i => i.Field(f => f.AelfAddress).Value(aelfAddress)));
        shouldQuery.Add(q => q.Term(i => i.Field(f => f.EvmAddress).Value(evmAddress)));
        QueryContainer Filter(QueryContainerDescriptor<AddressRelationshipIndex> f) => f.Bool(b => b.Should(shouldQuery));
        
        var res = await _addressRelationshipRepository.GetAsync(Filter);
        if (res != null && !res.Id.IsNullOrEmpty())
        {
            return true;
        }

        return false;
    }

    public async Task<string> GetAelfAddressByEvmAddressAsync(string evmAddress)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<AddressRelationshipIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i => i.Field(f => f.EvmAddress).Value(evmAddress)));
        QueryContainer Filter(QueryContainerDescriptor<AddressRelationshipIndex> f) => f.Bool(b => b.Must(mustQuery));
        
        var res = await _addressRelationshipRepository.GetAsync(Filter);
        return  res?.AelfAddress;
    }


    public async Task BindActivityAddressAsync(string aelfAddress, string sourceChainAddress, ChainType chainType,
        string activityId)
    {
        var index = new ActivityAddressIndex()
        {
            Id = IdGenerateHelper.GetId(activityId, aelfAddress),
            AelfAddress = aelfAddress,
            SourceChainAddress = sourceChainAddress,
            SourceChainType = chainType,
            ActivityId = activityId,
            CreatedTime = DateTime.UtcNow
        };
        
        await _activityAddressRepository.AddAsync(index);
    }

    public async Task<ActivityAddressIndex> GetActivityAddressAsync(string aelfAddress, string activityId)
    {
        if (aelfAddress.IsNullOrEmpty() || activityId.IsNullOrEmpty())
        {
            return new ActivityAddressIndex();
        }
        
        var mustQuery = new List<Func<QueryContainerDescriptor<ActivityAddressIndex>, QueryContainer>>
        {
            q => q.Term(i => i.Field(f => f.AelfAddress).Value(aelfAddress)),
            q => q.Term(i => i.Field(f => f.ActivityId).Value(activityId))
        };
        QueryContainer Filter(QueryContainerDescriptor<ActivityAddressIndex> f) => f.Bool(b => b.Must(mustQuery));
        
        var res = await _activityAddressRepository.GetAsync(Filter);
        
        return res;
    }

    public async Task<bool> CheckActivityBindingExistsAsync(string aelfAddress, string sourceChainAddress,
        string activityId)
    {
        if (aelfAddress.IsNullOrEmpty() && sourceChainAddress.IsNullOrEmpty() && activityId.IsNullOrEmpty())
        {
            return false;
        }

        var shouldQuery = new List<Func<QueryContainerDescriptor<ActivityAddressIndex>, QueryContainer>>();
        shouldQuery.Add(q => q.Term(i => i.Field(f => f.AelfAddress).Value(aelfAddress)));
        shouldQuery.Add(q => q.Term(i => i.Field(f => f.SourceChainAddress).Value(sourceChainAddress)));

        var mustQuery = new List<Func<QueryContainerDescriptor<ActivityAddressIndex>, QueryContainer>>
        {
            q => q.Term(i => i.Field(f => f.ActivityId).Value(activityId)),
            q => q.Bool(b => b.Should(shouldQuery))
        };
        QueryContainer Filter(QueryContainerDescriptor<ActivityAddressIndex> f) => f.Bool(b => b.Must(mustQuery));

        var res = await _activityAddressRepository.GetAsync(Filter);
        if (res != null && !res.Id.IsNullOrEmpty())
        {
            return true;
        }

        return false;
    }

    public async Task<string>  GetEvmAddressByAelfAddressAsync(string alefAddress)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<AddressRelationshipIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i => i.Field(f => f.AelfAddress).Value(alefAddress)));
        QueryContainer Filter(QueryContainerDescriptor<AddressRelationshipIndex> f) => f.Bool(b => b.Must(mustQuery));
        
        var res = await _addressRelationshipRepository.GetAsync(Filter);
        return  res?.EvmAddress;
    }
}