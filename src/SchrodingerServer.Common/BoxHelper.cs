namespace SchrodingerServer.Common;

public class BoxHelper
{
    public const string NonGen9Box = "https://schrodinger-mainnet.s3.amazonaws.com/d152a12d-3dd8-4e60-a25a-e1f83e4d30f6.png";
    public const string NormalBox = "https://schrodinger-mainnet.s3.amazonaws.com/9a853025-9543-470d-9de2-b7f91574013f.png";
    public const string BronzeBox = "https://schrodinger-mainnet.s3.amazonaws.com/acd8be98-e608-491f-b25d-ae122769bd84.png";
    public const string SilverBox = "https://schrodinger-mainnet.s3.amazonaws.com/5bc6f412-b9a9-4796-9e60-40c33d132431.png";
    public const string GoldBox = "https://schrodinger-mainnet.s3.amazonaws.com/4930e5bb-84b5-43a3-aa11-02b9b1b5bb0e.png";
    public const string PlatinumBox = "https://schrodinger-mainnet.s3.amazonaws.com/1b9221de-0ed3-4541-bd61-bec55dd22421.png";
    public const string EmeraldBox = "https://schrodinger-mainnet.s3.amazonaws.com/ba28a839-4e37-425f-8e04-048ad970de1a.png";
    public const string DiamondBox = "https://schrodinger-mainnet.s3.amazonaws.com/4cf172d5-8aba-48e9-9aa3-7ca716ffaf23.png";
    
    

    public static Dictionary<string, string> RarityImageDictionary = new Dictionary<string, string>
    {
        {"Normal", NormalBox},
        {"Bronze", BronzeBox},
        {"Silver", SilverBox},
        {"Gold", GoldBox},
        {"Platinum", PlatinumBox},
        {"Emerald", EmeraldBox},
        {"Diamond", DiamondBox}
    };
    
    public static string GetBoxImage(bool isGen9, string rarity)
    {
        if (!isGen9)
        {
            return NonGen9Box;
        }
        
        return RarityImageDictionary.TryGetValue(rarity, out var image) ? image : NormalBox;
    }
}