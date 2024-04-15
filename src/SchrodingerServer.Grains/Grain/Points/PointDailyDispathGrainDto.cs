namespace SchrodingerServer.Grains.Grain.Points;

public class PointDailyDispatchGrainDto
{
    public string Id { get; set; }
    
    public string BizDate { get; set; }

    public int Height{ get; set; }
    
    public DateTime CreateTime { get; set; }
    
    public bool Executed { get; set; }
    
}