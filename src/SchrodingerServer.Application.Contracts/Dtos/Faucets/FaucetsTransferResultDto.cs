namespace SchrodingerServer.Dtos.Faucets;

public class FaucetsTransferResultDto
{
    public string Address { get; set; }
    public long Amount { get; set; }
    public string Symbol { get; set; }
    public string TransactionId { get; set; }
}