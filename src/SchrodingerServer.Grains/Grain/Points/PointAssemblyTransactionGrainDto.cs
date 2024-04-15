using SchrodingerServer.Users.Dto;

namespace SchrodingerServer.Grains.Grain.Points;

public class PointAssemblyTransactionGrainDto
{
    public string Id { get; set; }
    
    public PointSettleDto PointSettleDto { get; set; }
    
    public DateTime CreateTime { get; set; }

    public static PointAssemblyTransactionGrainDto Of(string id, PointSettleDto pointSettleDto)
    {
        return new PointAssemblyTransactionGrainDto()
        {
            Id = id,
            PointSettleDto = pointSettleDto
        };
    }
}