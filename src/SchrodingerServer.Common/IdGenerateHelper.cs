namespace SchrodingerServer.Common;

public class IdGenerateHelper
{
    public static string GetId(params object[] inputs)
    {
        return inputs.JoinAsString("-");
    }
    
    public static string GetPointBizId(string chainId, string bizDate, string pointName, string guid)
    {
        return GetId(chainId, bizDate, pointName , guid);
    }
    
    public static string GetPointDailyRecord(string chainId, string bizDate, string pointName, string address)
    {
        return GetId(chainId, bizDate, pointName, address);
    }
    
    public static string GetHolderBalanceId(string chainId, string symbol, string address)
    {
        return GetId(chainId, symbol, address);
    }
}