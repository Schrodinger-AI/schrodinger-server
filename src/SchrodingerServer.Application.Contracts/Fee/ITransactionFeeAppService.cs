using System.Collections.Generic;

namespace SchrodingerServer.Fee;

public interface ITransactionFeeAppService
{
    TransactionFeeResultDto CalculateFee();
}