namespace SchrodingerServer.PointServer.Dto;

public class CheckDomainRequest
{
    public string Domain { get; set; } 
}

public class CheckDomainResponse
{
    public bool Exists { get; set;  }
}