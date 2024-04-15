using System.Collections.Generic;

namespace SchrodingerServer.Dto;

public class AwakenPriceRespDto
{
    public string Code { get; set; }
    public Data Data { get; set; }
    public string Message { get; set; }
}
public class Data
{
    public int TotalCount { get; set; }
    public List<Item> Items { get; set; }
}

public class Item
{
    public double ValueLocked0 { get; set; }
    public double ValueLocked1 { get; set; }
}

public class WhiteListResponse
{
    public string Code { get; set; }
    public CheckResult Data { get; set; }
    public string Message { get; set; }
}

public class CheckResult
{
    public bool IsAddressValid { get; set; }
}