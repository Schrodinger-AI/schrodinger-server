using System.Security.Cryptography;
using System.Text;

namespace SchrodingerServer.Common;

public static class GuidHelper
{
    
    /// <summary>
    ///     Generate an uniqId with params
    /// </summary>
    /// <param name="paramArr"></param>
    /// <returns></returns>
    public static Guid UniqGuid(params string[] paramArr)
    {
        return new Guid(MD5.HashData(Encoding.Default.GetBytes(GenerateId(paramArr))));
    }

    /// <summary>
    ///     Generate a string id
    /// </summary>
    /// <param name="paramArr"></param>
    /// <returns></returns>
    public static string GenerateId(params string[] paramArr)
    {
        return string.Join("_", paramArr);
    }
}