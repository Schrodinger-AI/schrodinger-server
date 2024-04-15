using System.Linq;
using Newtonsoft.Json;

namespace SchrodingerServer.Adopts.dispatcher;

public class ImageProviderHelper
{
    public static string ConvertObjectToJsonString<T>(T paramObj)
    {
        var paramMap = paramObj.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(paramObj, null));
        return JsonConvert.SerializeObject(paramMap);
    }

    public static string JoinAdoptIdAndAelfAddress(string adoptId, string aelfAddress)
    {
        return adoptId + "_" + aelfAddress;
    }
}