namespace SchrodingerServer.Grains.Grain.Points;

public class PointDailyRecordGrainBase
{
    public string Id { get; set; }

    public string ChainId { get; set; }
    
    public string PointName { get; set; }
    
    public string BizDate { get; set; }
    
    public string BizId { get; set; }
    
    public string Address { get; set; }
    
    public decimal PointAmount { get; set; }
    
    public string Status { get; set; }
    
    public DateTime CreateTime { get; set; }
    
    public DateTime UpdateTime { get; set; }
}