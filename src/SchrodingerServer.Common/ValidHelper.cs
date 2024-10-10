using System.Text.RegularExpressions;
using AElf;

namespace SchrodingerServer.Common;

public static class ValidHelper
{
    public const int MaxResultNumber = 200;
    private const char Underline = '_';

    private const string PatternLetters = @"^[A-Za-z]+$"; 
    private const string UppercaseNumericHyphen = @"^[A-Z0-9\-]+$"; 
    
    public static bool MatchesPattern(this string input, string pattern)
    {
        return Regex.IsMatch(input, pattern);
    }

    public static bool MatchesChainId(this string chainId)
    {
        return chainId.MatchesPattern(PatternLetters);
    }    

    public static bool MatchesNftSymbol(this string symbol)
    {
        return symbol.MatchesPattern(UppercaseNumericHyphen);
    }
}