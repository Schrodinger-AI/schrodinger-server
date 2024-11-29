using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using SchrodingerServer.SignatureServer.Common;
using SchrodingerServer.SignatureServer.Dtos;
using SchrodingerServer.SignatureServer.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.SignatureServer.Providers;

public interface ISignatureProvider
{
    SignResponseDto SignThirdPart(SignDto signDto);
}

public class SignatureProvider : ISignatureProvider, ISingletonDependency
{
    private readonly ILogger<SignatureProvider> _logger;
    private readonly IOptionsSnapshot<ThirdPartKeyStoreOptions> _thirdPartKeyStoreOptions;

    public SignatureProvider(ILogger<SignatureProvider> logger, 
        IOptionsSnapshot<ThirdPartKeyStoreOptions> thirdPartKeyStoreOptions)
    {
        _logger = logger;
        _thirdPartKeyStoreOptions = thirdPartKeyStoreOptions;
    }

    public SignResponseDto SignThirdPart(SignDto signDto)
    {
        try
        {
            var json = ReadThirdPartKeyStore(signDto.ApiKey);
            if (json == null) throw new ArgumentNullException(nameof(json));
            var keyStoreDocument = JObject.Parse(json);
            var apiSecret = keyStoreDocument["apiSecret"].Value<string>();
            if (apiSecret.IsNullOrWhiteSpace())
            {
                _logger.LogWarning("ThirdPart apiSecret not exists, key: {key}", signDto.ApiKey);
                throw new UserFriendlyException("ThirdPart apiSecret not exists");
            }

            return new SignResponseDto
            {
                Signature = SignHelper.GetSignature(apiSecret, signDto.PlainText)
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "ThirdPart Signature error");
            throw new UserFriendlyException("ThirdPart Signature error");
        }
    }
    
    private string ReadThirdPartKeyStore(string key)
    {
        var path = PathHelper.ResolvePath(_thirdPartKeyStoreOptions.Value.Path  + "/" + key + ".json");
        if (!File.Exists(path))
        {
            throw new UserFriendlyException("Thirdpart keystore file not exits: " + path);
        }

        using var textReader = File.OpenText(path);
        return textReader.ReadToEnd();
    }
}