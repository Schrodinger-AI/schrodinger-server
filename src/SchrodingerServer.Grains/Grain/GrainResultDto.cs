namespace SchrodingerServer.Grains.Grain;

[GenerateSerializer]
public class GrainResultDto<T>
{
    [Id(0)]
    public bool Success { get; set; } = true;
    [Id(1)]
    public string Message { get; set; } = string.Empty;

    [Id(2)]
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