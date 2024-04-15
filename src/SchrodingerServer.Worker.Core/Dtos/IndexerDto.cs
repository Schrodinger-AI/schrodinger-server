namespace SchrodingerServer.Worker.Core.Dtos;

public class IndexerConfirmedListDto
{
    public ConfirmedDto GetAdoptInfoList { get; set; }
}

public class ConfirmedDto
{
    public List<string> TransactionIds { get; set; }
}