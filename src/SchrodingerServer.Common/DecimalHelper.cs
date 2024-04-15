using System.Numerics;
using AElf.Types;

namespace SchrodingerServer.Common;

public class DecimalHelper
{
    public static int GetDecimalPlaces(decimal num)
    {
        var bits = decimal.GetBits(num);
        var scale = (bits[3] >> 16) & 0x7F;
        return scale;
    }

    public static long ConvertToLong(decimal num, int places)
    {
        var multiplier = (long)Math.Pow(10, places);
        return (long)(num * multiplier);
    }

    public static BigIntValue ConvertBigInteger(decimal num, int places)
    {
        var multiplier = (decimal)Math.Pow(10, places);
        var bigInt = new BigInteger(Math.Floor(num * multiplier));
        var result = new BigIntValue { Value = bigInt.ToString() };
        return result;
    }
    
    public static decimal MultiplyByPowerOfTen(decimal number, int n)
    {
        return number * (decimal)Math.Pow(10, n);
    }
    
    public static decimal Divide(decimal amount, int decimals)
    {
        return amount / (decimal)Math.Pow(10, decimals);
    }
    
    public static decimal? GetValueFromDict(Dictionary<string, decimal> priceDict, string key, string defaultKey)
    {
        if (priceDict.TryGetValue(key, out decimal value))
        {
            return value;
        }

        if (priceDict.TryGetValue(defaultKey, out value))
        {
            return value;
        }
        return null;
    }

}