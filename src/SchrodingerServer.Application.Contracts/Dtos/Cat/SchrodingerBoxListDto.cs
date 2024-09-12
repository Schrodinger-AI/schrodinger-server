using System.Collections.Generic;

namespace SchrodingerServer.Dtos.Cat;

public class SchrodingerBoxListDto
{
    public long TotalCount { get; set; }
    public List<BlindBoxDto> Data { get; set; }
}

public class BlindBoxDto
{
    public string Symbol { get; set; }
    public string TokenName { get; set; }
    public long Amount { get; set; }
    public int Generation { get; set; }
    public int Decimals { get; set; }
    public string Adopter { get; set; }
    public long AdoptTime { get; set; }
    // public string Level { get; set; }
    public string Rarity { get; set; }
    // public int Rank { get; set; }
    public string Describe { get; set; }
    public string InscriptionImageUri { get; set; }
    public List<TraitsInfo> Traits { get; set; }
}

public class BoxImageConst
{
    public const string RareBox = "https://schrodinger-mainnet.s3.amazonaws.com/fba1d3ce-3512-4a4e-8325-4bc3c49457c5.png";
    public const string NormalBox = "https://schrodinger-mainnet.s3.amazonaws.com/512a8d95-4d9b-40a5-9e11-446684e9db1e.png";
    public const string NonGen9Box = "https://schrodinger-mainnet.s3.amazonaws.com/d152a12d-3dd8-4e60-a25a-e1f83e4d30f6.png";
}