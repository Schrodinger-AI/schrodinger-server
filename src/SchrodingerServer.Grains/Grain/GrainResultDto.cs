namespace SchrodingerServer.Grains.Grain;

public class GrainResultDto<T>
{
    
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    
    public T Data { get; set; }


    public GrainResultDto()
    {
    }

    public GrainResultDto(T data)
    {
        Data = data;
    }

    public GrainResultDto<T> Error(string message)
    {
        Success = false;
        Message = message;
        return this;
    }
}