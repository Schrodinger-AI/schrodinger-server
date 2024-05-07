using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Client.Dto;
using AElf.Client.Service;
using AElf.Types;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using SchrodingerServer.Common.AElfSdk.Dtos;
using SchrodingerServer.Common.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Threading;

namespace SchrodingerServer.Common.AElfSdk;

public class ContractProvider : IContractProvider, ISingletonDependency
{
    private readonly Dictionary<string, AElfClient> _clients = new();
    private readonly Dictionary<string, string> _emptyDict = new();
    private readonly Dictionary<string, Dictionary<string, string>> _contractAddress = new();
    private readonly SenderAccount _callTxSender;

    private readonly ISecretProvider _secretProvider;

    private readonly IOptionsMonitor<ChainOptions> _chainOptions;
    private readonly ILogger<ContractProvider> _logger;
    
    public static readonly JsonSerializerSettings DefaultJsonSettings = JsonSettingsBuilder.New()
        .WithCamelCasePropertyNamesResolver()
        .WithAElfTypesConverters()
        .IgnoreNullValue()
        .Build();

    public ContractProvider(IOptionsMonitor<ChainOptions> chainOption, ILogger<ContractProvider> logger, ISecretProvider secretProvider)
    {
        _logger = logger;
        _secretProvider = secretProvider;
        _chainOptions = chainOption;
        InitAElfClient();
        _callTxSender = new SenderAccount(_chainOptions.CurrentValue.PrivateKeyForCallTx);
    }


    private void InitAElfClient()
    {
        if (_chainOptions.CurrentValue.ChainInfos.IsNullOrEmpty())
        {
            return;
        }

        foreach (var node in _chainOptions.CurrentValue.ChainInfos)
        {
            _clients[node.Key] = new AElfClient(node.Value.BaseUrl);
            _logger.LogInformation("init AElfClient: {ChainId}, {Node}", node.Key, node.Value.BaseUrl);
        }
    }

    private AElfClient Client(string chainId)
    {
        AssertHelper.IsTrue(_clients.ContainsKey(chainId), "AElfClient of {chainId} not found.", chainId);
        return _clients[chainId];
    }

    public string ContractAddress(string chainId, string contractName)
    {
        _ = _chainOptions.CurrentValue.ChainInfos.TryGetValue(chainId, out var chainInfo);
        var contractAddress = _contractAddress.GetOrAdd(chainId, _ => new Dictionary<string, string>());
        return contractAddress.GetOrAdd(contractName, name =>
        {
            var address = (chainInfo?.ContractAddress ?? new Dictionary<string, Dictionary<string, string>>())
                .GetValueOrDefault(chainId, _emptyDict)
                .GetValueOrDefault(name, null);
            if (address.IsNullOrEmpty() && SystemContractName.All.Contains(name))
                address = AsyncHelper
                    .RunSync(() => Client(chainId).GetContractAddressByNameAsync(HashHelper.ComputeFrom(name)))
                    .ToBase58();

            AssertHelper.NotEmpty(address, "Address of contract {contractName} on {chainId} not exits.",
                name, chainId);
            return address;
        });
    }

    public async Task SendTransactionAsync(string chainId, Transaction signedTransaction)
    {
        var client = Client(chainId);
        await client.SendTransactionAsync(new SendTransactionInput
        {
            RawTransaction = signedTransaction.ToByteArray().ToHex()
        });
    }
    
    public Task<TransactionResultDto> QueryTransactionResultAsync(string chainId, string transactionId)
    {
        return Client(chainId).GetTransactionResultAsync(transactionId);
    }
    
    public async Task<(Hash transactionId, Transaction transaction)> CreateCallTransactionAsync(string chainId,
        string contractName, string methodName, IMessage param)
    {
        var pair = await CreateTransactionAsync(chainId, _callTxSender.PublicKey.ToHex(), contractName, methodName,
            param);
        pair.transaction.Signature = _callTxSender.GetSignatureWith(pair.transaction.GetHash().ToByteArray());
        return pair;
    }

    public async Task<(Hash transactionId, Transaction transaction)> CreateTransactionAsync(string chainId,
        string senderPublicKey, string contractName, string methodName,
        IMessage param)
    {
        var address = ContractAddress(chainId, contractName);
        return await CreateTransactionAsync(chainId, senderPublicKey, address, methodName, param.ToByteString());
    }
    
    public async Task<(Hash transactionId, Transaction transaction)> CreateTransactionAsync(string chainId, string senderPublicKey, 
        string toAddress, string methodName, string paramBase64)
    {
        return await CreateTransactionAsync(chainId, senderPublicKey, toAddress, methodName, ByteString.FromBase64(paramBase64));
    }
    
    private async Task<(Hash transactionId, Transaction transaction)> CreateTransactionAsync(string chainId, string senderPublicKey, 
        string toAddress, string methodName, ByteString param)
    {
        var client = Client(chainId);
        var status = await client.GetChainStatusAsync();
        var height = status.BestChainHeight;
        var blockHash = status.BestChainHash;

        // create raw transaction
        _logger.LogInformation("CreateTransactionAsync, status: {status}, publickey:{pk}, toaddress:{address}, methodName:{methodName}", 
            JsonConvert.SerializeObject(status), senderPublicKey, toAddress, methodName);
        var transaction = new Transaction
        {
            From = Address.FromPublicKey(ByteArrayHelper.HexStringToByteArray(senderPublicKey)),
            To = Address.FromBase58(toAddress),
            MethodName = methodName,
            Params = param,
            RefBlockNumber = height,
            RefBlockPrefix = ByteString.CopyFrom(Hash.LoadFromHex(blockHash).Value.Take(4).ToArray())
        };
        
        _logger.LogInformation("CreateTransactionFinish, transaction: {transaction}", JsonConvert.SerializeObject(transaction));

        
        transaction.Signature = senderPublicKey == _callTxSender.PublicKey.ToHex() 
            ? _callTxSender.GetSignatureWith(transaction.GetHash().ToByteArray()) 
            : ByteString.CopyFrom(ByteArrayHelper.HexStringToByteArray(await _secretProvider.GetSignatureAsync(senderPublicKey, transaction)));
        _logger.LogInformation("CreateSignature, signature: {signature}", transaction.Signature.ToString());


        return (transaction.GetHash(), transaction);
    }
    
    public async Task<T> CallTransactionAsync<T>(string chainId, Transaction transaction) where T : class
    {
        var client = Client(chainId);
        // call invoke
        var rawTransactionResult = await client.ExecuteRawTransactionAsync(new ExecuteRawTransactionDto()
        {
            RawTransaction = transaction.ToByteArray().ToHex(),
            Signature = transaction.Signature.ToHex()
        });
        if (typeof(T) == typeof(string))
        {
            return rawTransactionResult?.Substring(1, rawTransactionResult.Length - 2) as T;
        }

        return (T)JsonConvert.DeserializeObject(rawTransactionResult, typeof(T), DefaultJsonSettings);
    }
    
}