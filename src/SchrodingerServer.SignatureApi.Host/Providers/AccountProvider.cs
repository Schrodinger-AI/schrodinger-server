using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AElf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.KeyStore;
using SchrodingerServer.SignatureServer.Common;
using SchrodingerServer.SignatureServer.Dtos;
using SchrodingerServer.SignatureServer.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.SignatureServer.Providers;

public class AccountProvider : ISingletonDependency
{
    private readonly ILogger<AccountProvider> _logger;
    private readonly Dictionary<string, AccountHolder> _accountHolders = new();
    private readonly IOptionsSnapshot<KeyStoreOptions> _keyStoreOptions;
    private readonly IOptionsSnapshot<KeyPairInfoOptions> _keyPairInfoOptions;
    private static readonly KeyStoreService KeyStoreService = new();

    public AccountProvider(IOptionsSnapshot<KeyStoreOptions> keyStoreOptions,
        IOptionsSnapshot<KeyPairInfoOptions> keyPairInfoOptions,
        ILogger<AccountProvider> logger)
    {
        _keyStoreOptions = keyStoreOptions;
        _keyPairInfoOptions = keyPairInfoOptions;
        _logger = logger;
        LoadKeyPair();
        LoadKeyStoreWithPassword();
        LoadKeyStoreWithInput();
    }

    public string GetSignature(string addressOrPublicKey, byte[] rawData)
    {
        var accountExists = _accountHolders.TryGetValue(addressOrPublicKey, out var account);
        if (!accountExists) throw new UserFriendlyException("Account not found");

        return ByteStringExtensions.ToHex(account.GetSignatureWith(rawData));
    }

    private void LoadKeyPair()
    {
        if (_keyPairInfoOptions.Value.PrivateKeyDictionary.IsNullOrEmpty()) return;
        foreach (var (publicKey, privateKey) in _keyPairInfoOptions.Value.PrivateKeyDictionary)
        {
            var account = new AccountHolder(privateKey);
            _logger.LogInformation("Load key pair success, address: {Address}", account.AddressObj().ToBase58());
            _accountHolders[account.PublicKey] = account;
            _accountHolders[account.AddressObj().ToBase58()] = account;
        }
    }

    private string ReadKeyStore(string address)
    {
        var path = PathHelper.ResolvePath(_keyStoreOptions.Value.Path + "/" + address + ".json");
        if (!File.Exists(path))
        {
            throw new UserFriendlyException("keystore file not exits " + path);
        }

        using var textReader = File.OpenText(path);
        return textReader.ReadToEnd();
    }


    private void LoadKeyStoreWithPassword()
    {
        if (_keyStoreOptions.Value.Path.IsNullOrEmpty()) return;
        if (_keyStoreOptions.Value.Passwords.IsNullOrEmpty()) return;
        foreach (var (address, password) in _keyStoreOptions.Value.Passwords)
        {
            var keyStoreContent = ReadKeyStore(address);
            var privateKey = KeyStoreService.DecryptKeyStoreFromJson(password, keyStoreContent);
            var account = new AccountHolder(HexByteConvertorExtensions.ToHex(privateKey));
            _logger.LogInformation("Load key store success, address: {Address}", address);
            _accountHolders[account.PublicKey] = account;
            _accountHolders[account.AddressObj().ToBase58()] = account;
        }
    }

    private void LoadKeyStoreWithInput()
    {
        if (_keyStoreOptions.Value.Path.IsNullOrEmpty()) return;
        if (_keyStoreOptions.Value.LoadAddress.IsNullOrEmpty()) return;
        var keyStoreDict = new Dictionary<string, string>();
        foreach (var address in _keyStoreOptions.Value.LoadAddress)
        {
            // account already exists, skip
            if (_accountHolders.ContainsKey(address)) continue;

            keyStoreDict[address] = ReadKeyStore(address);
        }

        _logger.LogInformation("Waiting for key store decode...");
        Task.Delay(1000);
        Console.WriteLine();
        Console.WriteLine("aaaaaaaaaaaaaaaaaaaaaaaaa Decode Key Store aaaaaaaaaaaaaaaaaaaaaaaaa");
        Console.WriteLine();
        Console.WriteLine(" Key store file path:     ");
        Console.WriteLine("\t" + PathHelper.ResolvePath(_keyStoreOptions.Value.Path));
        Console.WriteLine();
        Console.WriteLine(" Addresses will be loaded: ");
        foreach (var address in keyStoreDict.Keys)
        {
            Console.WriteLine("\t" + address);
        }

        Console.WriteLine();
        Console.WriteLine("aaaaaaaaaaaaaaaa  Press [Enter] to decode keystore  aaaaaaaaaaaaaaaa");
        Console.WriteLine();

        while (ConsoleKey.Enter != Console.ReadKey(true).Key)
        {
            // do nothing
        }

        foreach (var (address, json) in keyStoreDict)
        {
            AccountHolder account;
            while (!TryDecryptKeyStore(address, json, out account))
            {
            }

            _accountHolders[account.PublicKey] = account;
            _accountHolders[account.AddressObj().ToBase58()] = account;
        }

        Console.WriteLine();
        Console.WriteLine("aaaaaaaaaaaaaaaaaaaa  Finish to decode keystore  aaaaaaaaaaaaaaaaaaa");
        Console.WriteLine();
        _logger.LogInformation("Done!");
    }

    private bool TryDecryptKeyStore(string address, string json, out AccountHolder account)
    {
        try
        {
            /* read password from console */
            Console.Write($"Input password of {address}: ");
            var pwd = ReadPassword();
            var privateKey = KeyStoreService.DecryptKeyStoreFromJson(pwd, json);
            account = new AccountHolder(HexByteConvertorExtensions.ToHex(privateKey));
            Console.WriteLine("Success!");
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine("Failed!");
            account = null;
            return false;
        }
    }

    private static string ReadPassword()
    {
        var pwd = "";
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter)
                break;
            if (key.Key == ConsoleKey.Backspace && pwd.Length > 0)
                pwd = pwd[..^1];
            else if (key.Key != ConsoleKey.Backspace)
                pwd += key.KeyChar;
        }

        return pwd;
    }
}