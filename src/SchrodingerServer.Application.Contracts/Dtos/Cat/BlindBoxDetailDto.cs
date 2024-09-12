using System.Collections.Generic;

namespace SchrodingerServer.Dtos.Cat;

public class BlindBoxDetailDto
{
    public string Symbol { get; set; }
    public string TokenName { get; set; }
    public string InscriptionImageUri { get; set; }
    public int Generation { get; set; }
    public int Decimals { get; set; }
   
    public List<TraitDto> Traits { get; set; }
    public long HolderAmount{ get; set; }
    public long ConsumeAmount { get; set; }
    public bool DirectAdoption { get; set; }
    public string AdoptId { get; set; }
    public string Rarity { get; set; }
}