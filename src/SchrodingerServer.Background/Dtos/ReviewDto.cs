using System;
using System.Collections.Generic;

namespace SchrodingerServer.Background.Dtos;

public class ReviewDto
{
    public List<ReviewItem> Items { get; set; }
    public string NextCursor { get; set; }
}

public class ReviewItem
{
    public string Status { get; set; }
    public ReviewUser User { get; set; }
    public List<ReviewTask> Tasks { get; set; }
}

public class ReviewUser
{
    public string Id { get; set; }
}

public class ReviewTask
{
    public string Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Status { get; set; }
    public string Value { get; set; }
}