namespace SchrodingerServer.Options;

public class UniswapV3Options
{
    public string BaseUrl { get; set; } = "https://api.thegraph.com/subgraphs/name/uniswap/uniswap-v3";

    public string TokenId { get; set; }

    public string DefaultBasePrice { get; set; } = "0.5";

}