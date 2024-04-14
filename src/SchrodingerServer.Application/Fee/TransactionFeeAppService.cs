using Microsoft.Extensions.Options;
using SchrodingerServer.Common.Options;
using Volo.Abp;
using Volo.Abp.Auditing;

namespace SchrodingerServer.Fee;

[RemoteService(false)]
[DisableAuditing]
public class TransactionFeeAppService : SchrodingerServerAppService, ITransactionFeeAppService
{
    private readonly IOptionsMonitor<TransactionFeeOptions> _transactionFeeOptions;

    public TransactionFeeAppService(IOptionsMonitor<TransactionFeeOptions> transactionFeeOptions)
    {
        _transactionFeeOptions = transactionFeeOptions;
    }

    public TransactionFeeResultDto CalculateFee()
    {
        return new TransactionFeeResultDto() { TransactionFee = _transactionFeeOptions.CurrentValue.TransactionFee };
    }
}