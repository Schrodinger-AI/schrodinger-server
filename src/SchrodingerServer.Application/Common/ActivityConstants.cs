using System.Collections.Generic;

namespace SchrodingerServer.Common;

public class ActivityConstants
{
    public const string SGR5RankActivityId = "f7300bd9c9a8";
    public const string SGR7RankActivityId = "aea8b6b15364";
    
    
    public static Dictionary<string, decimal> LevelPriceDictionary = new Dictionary<string, decimal>
    {
        { "26", 64000 },

        { "25", 64000 },
        { "24", 64000 },
        { "23", 64000 },
        { "22", 64000 },
        { "21", 64000 },

        { "20", 64000 },
        { "19", 64000 },
        { "18", 64000 },
        { "17", 64000 },
        { "16", 64000 },

        { "15", 39552 },
        { "14", 19784 },
        { "13", 9892 },
        { "12", 4956 },
        { "11", 2488 },

        { "10", 1256 },
        { "9", 640 },
        { "8", 332 },
        { "7", 178 },
        { "6", 100 },

        { "5", 64 },
        { "4", 44 },
        { "3", 32 },
        { "2", 28 },
        { "1", 24 }
    };
}