using System.Collections.Generic;

namespace SchrodingerServer.Users.Index;


public class IndexerHolderDailyChangeDto
{
    public IndexerHolderDailyChanges GetSchrodingerHolderDailyChangeList { get; set; }
}

public class IndexerHolderDailyChanges
{
    public long TotalCount { get; set; }

    public List<HolderDailyChangeDto> Data { get; set; } = new();
}

public class HolderDailyChangeDto
{
    public string Address { get; set; }
    public string Symbol { get; set; }
    public string Date { get; set; }
    public long ChangeAmount { get; set; }
    public long Balance { get; set; }
}