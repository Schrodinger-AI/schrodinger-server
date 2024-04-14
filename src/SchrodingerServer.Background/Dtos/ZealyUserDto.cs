using System;

namespace SchrodingerServer.Background.Dtos;

public class ZealyUserDto
{
    public string Id { get; set; }
    public decimal Xp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}