namespace SchrodingerServer.Tasks.Dtos;

public class VoucherAdoptionDto
{
    public string VoucherId { get; set; }
    public string Rarity { get; set; }
    public int Rank { get; set; }
}

public class GetVoucherAdoptionQueryDto
{
    public VoucherAdoptionDto GetVoucherAdoption { get; set; }
}