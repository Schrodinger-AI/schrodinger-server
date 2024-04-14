using SchrodingerServer.Common;

namespace SchrodingerServer.Basic;

public class BasicResult<T>
{
    public int Code { get; set; } = BasicStatusCode.Success;
    public T Data { get; set; }
    public string Message { get; set; } = "success";
}