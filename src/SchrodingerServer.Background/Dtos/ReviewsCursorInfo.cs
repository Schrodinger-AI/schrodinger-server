using System;

namespace SchrodingerServer.Background.Dtos;

public class ReviewsCursorInfo
{
    public string NextCursor { get; set; }
    public DateTime UpdateTime { get; set; }
}