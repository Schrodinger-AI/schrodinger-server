using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Symbol.Index;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Symbol.Provider;


public interface ISymbolDayPriceProvider
{
  Task SaveSymbolDayPriceIndex(List<SymbolDayPriceIndex> symbolDayPriceIndex);

  Task<Dictionary<string, decimal>> GetSymbolPricesAsync(string bizDate, List<string> symbols);
}
public class SymbolDayPriceProvider : ISymbolDayPriceProvider, ISingletonDependency
{
    private readonly INESTRepository<SymbolDayPriceIndex, string> _symbolDayPriceIndexRepository;
    
    public  SymbolDayPriceProvider(INESTRepository<SymbolDayPriceIndex, string> symbolDayPriceIndexRepository)
    {
        _symbolDayPriceIndexRepository = symbolDayPriceIndexRepository;
    }

    public async Task SaveSymbolDayPriceIndex(List<SymbolDayPriceIndex> symbolDayPriceIndex)
    {
         await _symbolDayPriceIndexRepository.BulkAddOrUpdateAsync(symbolDayPriceIndex);
    }

    public async Task<Dictionary<string, decimal>> GetSymbolPricesAsync(string bizDate, List<string> symbols)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<SymbolDayPriceIndex>, QueryContainer>>();
        
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.Date).Value(bizDate)));
         
        mustQuery.Add(q => q.Terms(i =>
            i.Field(f => f.Symbol).Terms(symbols)));

        QueryContainer Filter(QueryContainerDescriptor<SymbolDayPriceIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var tuple = await _symbolDayPriceIndexRepository.GetSortListAsync(Filter);
        return !tuple.Item2.IsNullOrEmpty()
            ? tuple.Item2.ToDictionary(item => item.Symbol, item => item.Price)
            : new Dictionary<string, decimal>();
    }
}