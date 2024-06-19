using System;

namespace SchrodingerServer.AddressRelationship.Dto;

public class StageDto
{
    public StageTime InProgress { get; set; }
    public StageTime Displayed { get; set; }
}

public class StageTime 
{
    public long StartTime { get; set; }
    public long EndTime { get; set; }
}

public class StageTimeInDateTime
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}