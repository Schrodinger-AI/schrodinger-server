using SchrodingerServer.Users.Dto;

namespace SchrodingerServer.Grains.Grain.Points;

[GenerateSerializer]
public class PointAssemblyTransactionGrainDto
{
    [Id(0)]
    public string Id { get; set; }

    [Id(1)]
    public PointSettleDto PointSettleDto { get; set; }

    [Id(2)]
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