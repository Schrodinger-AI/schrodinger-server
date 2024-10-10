using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;

namespace SchrodingerServer.ExceptionHandling;

public class ExceptionHandlingService
{
    public static async Task<FlowBehavior> HandleException(Exception ex)
    {
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = true
        };
    }
    
    public static async Task<FlowBehavior> HandleExceptionDefault(Exception ex)
    {
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return
        };
    }
    
    public static async Task<FlowBehavior> HandleExceptionString(Exception ex)
    {
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = ""
        };
    }
    
    public static async Task<FlowBehavior> HandleExceptionNull(Exception ex)
    {
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = null
        };
    }
    
    public static async Task<FlowBehavior> HandleExceptionFalse(Exception ex)
    {
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
    
    
    
}