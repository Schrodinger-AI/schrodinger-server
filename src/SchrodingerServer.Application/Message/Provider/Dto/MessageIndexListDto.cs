using System;
using System.Collections.Generic;

namespace SchrodingerServer.Message.Provider.Dto;

public class MessageIndexListDto
{
    public long TotalCount { get; set; }
    public List<MessageIndexDto> Data { get; set; }
}

public class MessageIndexDto
{
    public string Symbol { get; set; }
    public string Address { get; set; }
    public DateTime CreatedTime { get; set; }
}
