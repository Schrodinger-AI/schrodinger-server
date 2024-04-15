namespace SchrodingerServer.GateIo.Dtos;

public class EthApiResponse
{
    public string Status { get; set; }
    public string Message { get; set; }
    public string Result { get; set; }
}

public class EthApiResponseConstant
{
    public const string SuccessStatus = "1";
    public const string SuccessMessage = "OK";
}