namespace SchrodingerServer.Grains.Grain.ApplicationHandler;

public static class MethodName
{
    public const string Transfer = "Transfer";
    public const string GetBalance = "GetBalance";
    public const string GetParentChainHeight = "GetParentChainHeight";
    public const string GetSideChainHeight = "GetSideChainHeight";
    public const string CrossChainCreateToken = "CrossChainCreateToken";
    public const string ValidateTokenInfoExists = "ValidateTokenInfoExists";
    public const string GetTokenInfo = "GetTokenInfo";

    public const string GetBoundParentChainHeightAndMerklePathByHeight =
        "GetBoundParentChainHeightAndMerklePathByHeight";
}

public static class TransactionState
{
    public const string Mined = "MINED";
    public const string Pending = "PENDING";
    public const string Notexisted = "NOTEXISTED";
}