using AElf.Client.Dto;
using AElf.Types;
using Google.Protobuf;
using SchrodingerServer.Common.Dtos;

namespace SchrodingerServer.Common;

public interface IContractProvider
{
    Task<(Hash transactionId, Transaction transaction)> CreateCallTransactionAsync(string chainId,
        string contractName, string methodName, IMessage param);

    Task<(Hash transactionId, Transaction transaction)> CreateTransactionAsync(string chainId, string senderPublicKey,
        string contractName, string methodName, IMessage param);

    Task<(Hash transactionId, Transaction transaction)> CreateTransactionAsync(string chainId, string senderPublicKey,
        string toAddress, string methodName, string paramBase64);

    string ContractAddress(string chainId, string contractName);

    Task SendTransactionAsync(string chainId, Transaction signedTransaction);

    Task<T> CallTransactionAsync<T>(string chainId, Transaction transaction) where T : class;

    Task<TransactionResultDto> QueryTransactionResultAsync(string chainId, string transactionId);
    Task<CheckTransactionDto> CheckTransactionStatusAsync(string transactionId, string chainId);
    Task<SendTransactionOutput> SendTransactionWithRetAsync(string chainId, Transaction signedTransaction);
}