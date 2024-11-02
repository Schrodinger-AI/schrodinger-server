using System.Collections.Generic;

namespace SchrodingerServer.Tasks.Dtos;

public class LogTgBotInput
{
    public string Address { get; set; }
    public string UserId { get; set; }
    public string Username { get; set; }
    public string From { get; set; }
    public string Language { get; set; }
    public long LoginTime { get; set; }
    public decimal Score { get; set; }
    public Dictionary<string, string> ExtraData { get; set; }
}

public class AddVoucherInput
{
    public string Address { get; set; }
    public string ChainId { get; set; }
}