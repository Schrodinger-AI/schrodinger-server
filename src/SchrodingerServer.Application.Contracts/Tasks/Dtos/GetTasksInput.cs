using System.Collections.Generic;

namespace SchrodingerServer.Tasks.Dtos;

public class GetTasksInput
{
    public string Address { get; set; }
    public List<string> TaskIdList { get; set; }
    public string Date { get; set; }
}

