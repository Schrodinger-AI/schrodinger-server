using AElf.Indexing.Elasticsearch;
using Nest;
using SchrodingerServer.Entities;

namespace SchrodingerServer.Zealy;

public class ZealyXpScoreIndex: SchrodingerEntity<string>, IIndexBuild
{
    [Keyword] public override string Id { get; set; }
    public decimal LastRawScore { get; set; }
    public decimal LastActualScore { get; set; }
    public decimal RawScore { get; set; }
    public decimal ActualScore { get; set; }
    public long CreateTime { get; set; }
    public long UpdateTime { get; set; }
}