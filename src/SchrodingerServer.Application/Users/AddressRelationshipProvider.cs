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
    
    Task<string> GetEvmAddressByAelfAddressAsync(string alefAddress);
}


public class AddressRelationshipProvider : IAddressRelationshipProvider, ISingletonDependency
{
    private readonly INESTRepository<AddressRelationshipIndex, string> _addressRelationshipRepository;
    private readonly IClusterClient _clusterClient;
    private readonly IObjectMapper _objectMapper;
    
    public AddressRelationshipProvider(
        INESTRepository<AddressRelationshipIndex, string> addressRelationshipRepository,
        IClusterClient clusterClient,
        IObjectMapper objectMapper)
    {
        _addressRelationshipRepository = addressRelationshipRepository;
        _clusterClient = clusterClient;
        _objectMapper = objectMapper;
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
    
    public async Task<string>  GetEvmAddressByAelfAddressAsync(string alefAddress)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<AddressRelationshipIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i => i.Field(f => f.AelfAddress).Value(alefAddress)));
        QueryContainer Filter(QueryContainerDescriptor<AddressRelationshipIndex> f) => f.Bool(b => b.Must(mustQuery));
        
        var res = await _addressRelationshipRepository.GetAsync(Filter);
        return  res?.EvmAddress;
    }
}