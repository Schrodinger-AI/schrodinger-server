using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AElf.Types;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SchrodingerServer.Common.Dtos;
using SchrodingerServer.Common.HttpClient;
using SchrodingerServer.Common.Options;
using Volo.Abp.DependencyInjection;
using HttpMethod = System.Net.Http.HttpMethod;

namespace SchrodingerServer.Common;

public class SecretProvider : ISecretProvider, ITransientDependency
{
    private const string GetSecurityUri = "/api/app/thirdPart/secret";
    private const string GetSignatureUri = "/api/app/signature";


    private readonly ILogger<SecretProvider> _logger;
    private readonly IOptionsMonitor<SecurityServerOptions> _securityOption;
    private readonly ILocalMemoryCache<string> _secretCache;
    private readonly IHttpProvider _httpProvider;

    public SecretProvider(ILogger<SecretProvider> logger, IOptionsMonitor<SecurityServerOptions> securityOption,
        ILocalMemoryCache<string> secretCache, IHttpProvider httpProvider)
    {
        _logger = logger;
        _securityOption = securityOption;
        _secretCache = secretCache;
        _httpProvider = httpProvider;
    }

    private string Uri(string path)
    {
        return _securityOption.CurrentValue.BaseUrl.TrimEnd('/') + path;
    }

    /// <summary>
    ///     
    /// </summary>
    /// <param name="publicKey"></param>
    /// <param name="transaction"></param>
    /// <returns></returns>
    public async Task<string> GetSignatureAsync(string publicKey, Transaction transaction)
    {
        var signatureSend = new SendSignatureDto
        {
            PublicKey = publicKey,
            HexMsg = transaction.GetHash().ToHex(),
        };

        var resp = await _httpProvider.InvokeAsync<CommonResponseDto<SignResponseDto>>(HttpMethod.Post,
            Uri(GetSignatureUri), 
            body: JsonConvert.SerializeObject(signatureSend),
            header: SecurityServerHeader()
        );
        AssertHelper.IsTrue(resp?.Success ?? false, "Signature response failed");
        AssertHelper.NotEmpty(resp!.Data?.Signature, "Signature response empty");
        return resp.Data!.Signature;
    }
    
    [ExceptionHandler(typeof(Exception), Message = "CallSignatureServiceFailed error", ReturnDefault = ReturnDefault.Default)]
    public async Task<string> GetSignatureFromHashAsync(string publicKey, Hash hash)
    {
        var signatureSend = new SendSignatureDto
        {
            PublicKey = publicKey,
            HexMsg = hash.ToHex(),
        };

        var url = Uri(GetSignatureUri);
        var resp = await _httpProvider.InvokeAsync<CommonResponseDto<SignResponseDto>>(HttpMethod.Post,
            url,
            body: JsonConvert.SerializeObject(signatureSend),
            header: SecurityServerHeader()
        );
        AssertHelper.IsTrue(resp?.Success ?? false, "Signature response failed");
        AssertHelper.NotEmpty(resp!.Data?.Signature, "Signature response empty");
        return resp.Data!.Signature;
    }
    

    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public async Task<string> GetSecretWithCacheAsync(string key)
    {
        return await _secretCache.GetOrAddAsync(key, async () => await GetSecretAsync(key), new MemoryCacheEntryOptions
        {
            AbsoluteExpiration =
                DateTimeOffset.Now.AddSeconds(_securityOption.CurrentValue.SecretCacheSeconds)
        });
    }

    private async Task<string> GetSecretAsync(string key)
    {
        var resp = await _httpProvider.InvokeAsync<CommonResponseDto<string>>(HttpMethod.Get,
            Uri(GetSecurityUri),
            param: new Dictionary<string, string>
            {
                ["key"] = key
            },
            header: SecurityServerHeader(key));
        AssertHelper.NotEmpty(resp?.Data, "Secret response data empty");
        AssertHelper.IsTrue(resp!.Success, "Secret response failed {}", resp.Message);
        return EncryptionHelper.DecryptFromHex(resp!.Data, _securityOption.CurrentValue.AppSecret);
    }
    
    public Dictionary<string, string> SecurityServerHeader(params string[] signValues)
    {
        var signString = string.Join(CommonConstant.EmptyString, signValues);
        return new Dictionary<string, string>
        {
            ["appid"] = _securityOption.CurrentValue.AppId,
            ["signature"] = EncryptionHelper.EncryptHex(signString, _securityOption.CurrentValue.AppSecret)
        };
    }
    
    

    public class SendSignatureDto
    {
        public string PublicKey { get; set; }
        public string HexMsg { get; set; }
    }

    public class SignResponseDto
    {
        public string Signature { get; set; }
    }
}