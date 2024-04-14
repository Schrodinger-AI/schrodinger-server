using System;
using SchrodingerServer.Common;
using Xunit;

namespace SchrodingerServer;

public class HelperTest
{
    
    [Fact]
    public static void ConvertToLong_Test()
    {
        var l = DecimalHelper.ConvertToLong(3053.9514m, 0);
        Console.WriteLine(l);
    }
    
    
    
    [Fact]
    public static void ConvertBigInteger_Test()
    {
        var num = decimal.Parse("228162514264337593543950335.9");
        var l = DecimalHelper.ConvertBigInteger(num, 0);
        Console.WriteLine(l);
    }

}