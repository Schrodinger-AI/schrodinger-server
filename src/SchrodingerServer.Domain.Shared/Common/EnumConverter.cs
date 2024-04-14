using System;

namespace SchrodingerServer.Common;

public class EnumConverter
{
    public static T ConvertToEnum<T>(string value) where T : struct
    {
        if (!typeof(T).IsEnum)
        {
            throw new ArgumentException("T must be an enumerated type");
        }

        if (Enum.TryParse<T>(value, true, out T result))
        {
            return result;
        }

        throw new ArgumentException($"Invalid value for enum {typeof(T).Name}: {value}");
    }
}