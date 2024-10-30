using System.Collections.Generic;

namespace SchrodingerServer.Tasks.Dtos;

public class FinishInput
{
    public string TaskId { get; set; }
    public string Address { get; set; }
    public Dictionary<string, string> ExtraData { get; set; } = new();
}

public class ClaimInput
{
    public string TaskId { get; set; }
    public string Address { get; set; }
}