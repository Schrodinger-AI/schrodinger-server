using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Client.Service;
using AElf.Types;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Schrodinger;
using SchrodingerServer.Common;
using SchrodingerServer.Common.AElfSdk;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Options;
using Serilog;
using Xunit;

namespace SchrodingerServer.Provider;

public class ContractProviderTest
{
    private readonly Mock<ILogger<ContractProvider>> _mockLogger = new();
    private readonly Mock<ISecretProvider> _mockSecretProvider = new();
    private readonly Mock<IOptionsMonitor<ChainOptions>> _mockChainOptions = new();
    private readonly Mock<AElfClient> _mockAElfClient = new();

    private const string ChainId = "tDVW";
    private const string ContractAddress = "Ccc5pNs71BMbgDr2ZwpNqtegfkHkBsTJ57HBZ6gw3HNH6pb9S";
    private const string BaseUrl = "https://tdvw-test-node.aelf.io";

    private const string ContractMethod = "BatchSettle";
    private const string UserAddress = "2GmpGegBTsjDmoVxu1n4nZvxezyc9GKVQCzWYm193iivncv7GU";

    private const string PrivateKey = "baf36c247544e5ba36c0c8ae73ae5eddd0b60c816f9c871c6dfcd44924a86d97";
    private const string PublicKey =
        "04726cca6c368ea2b9e0f81ad19db37b8374f0b17078cd20cefa766040235491b72e6920c622d5cfd357dbeb7ebf20bbcb232724eb71c1d31c6f6e1d0c5e4a5412";

    public ContractProviderTest()
    {
        _mockChainOptions.Setup(m => m.CurrentValue).Returns(new ChainOptions
        {
            ChainInfos = new Dictionary<string, ChainOptions.ChainInfo>
            {
                { ChainId, new ChainOptions.ChainInfo { BaseUrl =  BaseUrl} }
            }
        });

        _mockSecretProvider.Setup(se => se.GetSignatureAsync(It.IsAny<string>(), 
                It.IsAny<Transaction>()))
            .Returns(Task.FromResult(""));
    }

    [Fact]
    public async Task<(Hash transactionId, Transaction transaction)> CreateTransactionAsync_Test()
    {
        // Arrange
        var contractProvider =
            new ContractProvider(_mockChainOptions.Object, _mockLogger.Object, _mockSecretProvider.Object);

        //Of paramBase64
        var paramBase64 = MockParamBase64();
        
        //mock signature
        _mockSecretProvider.Setup(se => se.GetSignatureAsync(It.IsAny<string>(), 
                It.IsAny<Transaction>()))
            .Returns(MockGetSignature(ContractAddress, ContractMethod, paramBase64));
        
        // Act
        var result = await contractProvider.CreateTransactionAsync(ChainId, PublicKey, ContractAddress, ContractMethod, paramBase64);

        // Assert
        Assert.NotNull(result.transaction);
        Assert.NotNull(result.transactionId);
        
        var signedTransaction = result.transaction;
        var txId = signedTransaction.GetHash().ToHex();
        var refBlockNumber = signedTransaction.RefBlockNumber;
        var sender = signedTransaction.From.ToBase58();
        
        _mockLogger.Object.LogInformation("txId {txId} refBlockNumber{refBlockNumber} Sender {Sender}", 
            txId, refBlockNumber, sender);

        return result;
    }
    
    [Fact]
    public async Task SendTransactionAsync_Test()
    {
        var result = await CreateTransactionAsync_Test();

        // Arrange
        var contractProvider =
            new ContractProvider(_mockChainOptions.Object, _mockLogger.Object, _mockSecretProvider.Object);

        await contractProvider.SendTransactionAsync(ChainId, result.transaction);

        _mockLogger.Object.LogInformation("transactionId {transactionId} ", result.transactionId.ToHex()); 
        await Task.Delay(5000);

        var transactionResult = await contractProvider.QueryTransactionResultAsync(ChainId, result.transactionId.ToHex());
        _mockLogger.Object.LogInformation("transactionResult {transactionResult} ", transactionResult);
        Assert.NotNull(transactionResult.Status);
        Assert.Equal("MINED", transactionResult.Status);
    }

    private string MockParamBase64()
    {
        var userPoints = new UserPoints
        {
            UserAddress = Address.FromBase58(UserAddress),
            UserPointsValue = DecimalHelper.ConvertBigInteger(29.12312m, 0)
        };

        var batchSettleInput = new BatchSettleInput()
        {
            ActionName = "Trade",
            UserPointsList = { userPoints }
        };
        return batchSettleInput.ToByteString().ToBase64();
    }
    
    private async Task<string> MockGetSignature(string toAddress, string methodName, string paramBase64)
    {
        var client = new AElfClient(BaseUrl);
        var status = await client.GetChainStatusAsync();
        var height = status.BestChainHeight;
        var blockHash = status.BestChainHash;
        var from = client.GetAddressFromPrivateKey(PrivateKey);
        var transaction = new Transaction
        {
            From = Address.FromBase58(from),
            To = Address.FromBase58(toAddress),
            MethodName = methodName,
            Params = ByteString.FromBase64(paramBase64),
            RefBlockNumber = height,
            RefBlockPrefix = ByteString.CopyFrom(Hash.LoadFromHex(blockHash).Value.Take(4).ToArray())
        };
        return client.SignTransaction(PrivateKey, transaction).Signature.ToHex();
    }
}