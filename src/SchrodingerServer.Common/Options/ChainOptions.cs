namespace SchrodingerServer.Common.Options;

public class ChainOptions
{

    public string PrivateKeyForCallTx { get; set; }= "838183d5cf676d17a3aa8daff3c70952d27285101509fcb686c74b7e9d200d62";
    public Dictionary<string, ChainInfo> ChainInfos { get; set; } = new();
    
    public int MaxRetryCount { get; set; } = 5;

    public int BlockPackingMaxHeightDiff { get; set; } = 512;
    
    public int TokenImageRefreshDelaySeconds { get; set; } = 300;
    
    public class ChainInfo
    {
        public string BaseUrl { get; set; }
        public bool IsMainChain { get; set; }
        public string PrivateKey { get; set; }
        public string PointTxPublicKey { get; set; }
        public string FaucetsPublicKey { get; set; }
        public string TokenContractAddress { get; set; }
        public string CrossChainContractAddress { get; set; }
        public List<string> ManagerAccountPublicKeys { get; set; }
        public Dictionary<string, Dictionary<string, string>> ContractAddress { get; set; } = new();
    }
   
    public string PublicKey { get; set; }
}
