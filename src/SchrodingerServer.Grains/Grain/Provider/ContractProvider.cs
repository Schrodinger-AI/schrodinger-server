using AElf;
using AElf.Client.Dto;
using AElf.Client.Proto;
using AElf.Client.Service;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Options;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Grains.Grain.ApplicationHandler;
using SchrodingerServer.Signature.Provider;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Grains.Grain.Provider;

public interface IContractProvider
{
    public Task<long> GetBlockLatestHeightAsync(string chainId);
    public Task<TransactionResultDto> GetTxResultAsync(string chainId, string transactionId);
    public T ParseLogEvents<T>(TransactionResultDto txResult) where T : class, IMessage<T>, new();
    public Task<MerklePathDto> GetMerklePathAsync(string chainId, string txId);
    public Task<long> GetIndexHeightAsync(string chainId);
    public Task<long> GetSideChainIndexHeightAsync(string chainId, string sourceChainId);
    public Task<CrossChainMerkleProofContext> GetCrossChainMerkleProofContextAsync(string chainId, long blockHeight);
    public Task<TokenInfo> GetTokenInfoAsync(string chainId, string symbol);
    public Task<(string, string)> SendValidateTokenExistAsync(string chainId, TokenInfo tokenInfo);
    public Task<string> CrossChainCreateToken(string chainId, CrossChainCreateTokenInput createTokenParams);
}

public class ContractProvider : IContractProvider, ISingletonDependency
{
    private readonly ISignatureProvider _signatureProvider;
    private readonly IOptionsMonitor<ChainOptions> _chainOptions;
    private readonly IBlockchainClientFactory<AElfClient> _blockchainClientFactory;

    public ContractProvider(IBlockchainClientFactory<AElfClient> blockchainClientFactory,
        IOptionsMonitor<ChainOptions> chainOptions, ISignatureProvider signatureProvider)
    {
        _chainOptions = chainOptions;
        _signatureProvider = signatureProvider;
        _blockchainClientFactory = blockchainClientFactory;
    }

    # region Call

    public async Task<long> GetBlockLatestHeightAsync(string chainId)
        => await _blockchainClientFactory.GetClient(chainId).GetBlockHeightAsync();

    public async Task<TransactionResultDto> GetTxResultAsync(string chainId, string transactionId)
        => await _blockchainClientFactory.GetClient(chainId).GetTransactionResultAsync(transactionId);

    public async Task<MerklePathDto> GetMerklePathAsync(string chainId, string txId)
        => await _blockchainClientFactory.GetClient(chainId).GetMerklePathByTransactionIdAsync(txId);

    public async Task<long> GetIndexHeightAsync(string chainId)
    {
        var chainInfo = _chainOptions.CurrentValue.ChainInfos[chainId];
        var client = _blockchainClientFactory.GetClient(chainId);

        return Int64Value.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(await client.ExecuteTransactionAsync(
            new ExecuteTransactionDto
            {
                RawTransaction = await GenerateRawTransactionAsync(MethodName.GetParentChainHeight, new Empty(),
                    chainId, chainInfo.CrossChainContractAddress)
            }))).Value;
    }

    public async Task<long> GetSideChainIndexHeightAsync(string chainId, string sourceChainId)
    {
        var chainInfo = _chainOptions.CurrentValue.ChainInfos[chainId];
        var client = _blockchainClientFactory.GetClient(chainId);

        return Int64Value.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(await client.ExecuteTransactionAsync(
            new ExecuteTransactionDto
            {
                RawTransaction = await GenerateRawTransactionAsync(MethodName.GetSideChainHeight,
                    new Int32Value { Value = ChainHelper.ConvertBase58ToChainId(sourceChainId) }, chainId,
                    chainInfo.CrossChainContractAddress)
            }))).Value;
    }

    public async Task<CrossChainMerkleProofContext> GetCrossChainMerkleProofContextAsync(string chainId,
        long blockHeight)
    {
        var chainInfo = _chainOptions.CurrentValue.ChainInfos[chainId];
        var client = _blockchainClientFactory.GetClient(chainId);

        var result = await client.ExecuteTransactionAsync(new ExecuteTransactionDto
        {
            RawTransaction = await GenerateRawTransactionAsync(
                MethodName.GetBoundParentChainHeightAndMerklePathByHeight, new Int64Value { Value = blockHeight },
                chainId, chainInfo.CrossChainContractAddress)
        });
        return CrossChainMerkleProofContext.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(result));
    }

    public async Task<TokenInfo> GetTokenInfoAsync(string chainId, string symbol)
        => await CallTransactionAsync<TokenInfo>(chainId, await GenerateRawTransactionAsync(MethodName.GetTokenInfo,
            new GetTokenInfoInput { Symbol = symbol }, chainId,
            _chainOptions.CurrentValue.ChainInfos[chainId].TokenContractAddress));

    # endregion

    # region Send

    public async Task<(string, string)> SendValidateTokenExistAsync(string chainId, TokenInfo tokenInfo)
    {
        var validateTokenTx = await GenerateRawTransactionAsync(MethodName.ValidateTokenInfoExists,
            new ValidateTokenInfoExistsInput
            {
                Symbol = tokenInfo.Symbol,
                TokenName = tokenInfo.TokenName,
                Decimals = tokenInfo.Decimals,
                IsBurnable = tokenInfo.IsBurnable,
                IssueChainId = tokenInfo.IssueChainId,
                Issuer = new AElf.Types.Address { Value = tokenInfo.Issuer.Value },
                TotalSupply = tokenInfo.TotalSupply,
                Owner = tokenInfo.Owner,
                ExternalInfo = { tokenInfo.ExternalInfo.Value }
            }, chainId, _chainOptions.CurrentValue.ChainInfos[chainId].TokenContractAddress);

        var validateTokenTxId = (await SendTransactionAsync(chainId, validateTokenTx)).TransactionId;

        return (validateTokenTx, validateTokenTxId);
    }

    public async Task<string> CrossChainCreateToken(string chainId, CrossChainCreateTokenInput createTokenParams)
        => (await SendTransactionAsync(chainId,
            await GenerateRawTransactionAsync(MethodName.CrossChainCreateToken, createTokenParams,
                chainId, _chainOptions.CurrentValue.ChainInfos[chainId].TokenContractAddress))).TransactionId;

    #endregion

    # region Common

    private async Task<SendTransactionOutput> SendTransactionAsync(string chainId, string rawTx)
        => await _blockchainClientFactory.GetClient(chainId)
            .SendTransactionAsync(new SendTransactionInput { RawTransaction = rawTx });

    private async Task<string> GenerateRawTransactionAsync(string methodName, IMessage param, string chainId,
        string contractAddress)
    {
        var accounts = _chainOptions.CurrentValue.ChainInfos[chainId].ManagerAccountPublicKeys;
        var currentAccount = accounts[new Random().Next() % accounts.Count];
        var client = _blockchainClientFactory.GetClient(chainId);
        var ownAddress = client.GetAddressFromPubKey(currentAccount); //select public key
        var transaction = await client.GenerateTransactionAsync(ownAddress, contractAddress, methodName, param);

        var signature = await _signatureProvider.SignTxMsg(currentAccount, transaction.GetHash().ToHex());
        transaction.Signature = ByteStringHelper.FromHexString(signature);

        return transaction.ToByteArray().ToHex();
    }

    private async Task<T> CallTransactionAsync<T>(string chainId, string rawTx) where T : class, IMessage<T>, new()
    {
        var client = _blockchainClientFactory.GetClient(chainId);
        var result = await client.ExecuteTransactionAsync(new ExecuteTransactionDto { RawTransaction = rawTx });
        var value = new T();
        value.MergeFrom(ByteArrayHelper.HexStringToByteArray(result));
        return value;
    }

    public T ParseLogEvents<T>(TransactionResultDto txResult) where T : class, IMessage<T>, new()
    {
        var log = txResult.Logs.FirstOrDefault(l => l.Name == typeof(T).Name);
        var transactionLogEvent = new T();
        if (log == null) return transactionLogEvent;

        var logEvent = new LogEvent
        {
            Indexed = { log.Indexed.Select(ByteString.FromBase64) },
            NonIndexed = ByteString.FromBase64(log.NonIndexed)
        };
        transactionLogEvent.MergeFrom(logEvent.NonIndexed);
        foreach (var indexed in logEvent.Indexed)
        {
            transactionLogEvent.MergeFrom(indexed);
        }

        return transactionLogEvent;
    }

    #endregion
}