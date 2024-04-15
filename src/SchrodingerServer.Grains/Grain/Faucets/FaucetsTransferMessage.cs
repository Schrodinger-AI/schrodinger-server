namespace SchrodingerServer.Grains.Grain.Faucets;

public class FaucetsTransferMessage
{
    public const string TransferRestrictionsMessage =
        "Sorry! To be fair to all developers, each address is only allowed to receive it once.";

    public const string InvalidAddressMessage = "Invalid Address.";
    public const string SuspendUseMessage = "Suspend use.";
    public const string TransferPendingMessage = "The last faucet transaction is pending, please try again later.";
}