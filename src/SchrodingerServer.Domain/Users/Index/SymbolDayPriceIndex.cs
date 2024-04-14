using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.Symbol.Index;

public class SymbolDayPriceIndex: SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public string Symbol { get; set; }
    
    [Keyword] public string Date { get; set; }
    
    public decimal Price { get; set; }
}