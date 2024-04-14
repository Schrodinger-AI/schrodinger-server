using System;
using System.Collections.Generic;
using System.Linq;

namespace SchrodingerServer.Common;

public class PaginationHelper
{
    public static List<T> Paginate<T>(List<T> source, int skipCount, int maxResultCount)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (maxResultCount < 0)
            throw new ArgumentOutOfRangeException(nameof(maxResultCount), "Page size must be greater than 0.");
        
        return source.Skip(skipCount).Take(maxResultCount).ToList();
    }
}