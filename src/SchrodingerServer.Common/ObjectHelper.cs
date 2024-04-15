using JetBrains.Annotations;

namespace SchrodingerServer.Common;

public class ObjectHelper
{
    public static string ConvertObjectToSortedString([CanBeNull] object obj, params string[] ignoreParams)
    {
        if (obj == null) return string.Empty;
        var dict = new SortedDictionary<string, object>();

        if (obj is IDictionary<string, string> inputDict)
        {
            foreach (var kvp in inputDict)
            {
                if (ignoreParams.Contains(kvp.Key)) continue;
                if (kvp.Value == null) continue; // ignore null value
                dict[kvp.Key] = kvp.Value;
            }
        }
        else
        {
            foreach (var property in obj.GetType().GetProperties())
            {
                var key = property.Name.Substring(0, 1).ToLower() + property.Name.Substring(1);
                if (!property.CanRead || ignoreParams.Contains(key)) continue;

                var value = property.GetValue(obj);
                if (value == null) continue; // ignore null value

                dict[key] = value;
            }
        }
        return string.Join("&", dict.Select(kv => kv.Key + "=" + kv.Value));
    }
}