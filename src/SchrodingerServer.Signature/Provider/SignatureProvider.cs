using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Dtos;
using SchrodingerServer.Common.Http;
using SchrodingerServer.Signature.Options;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Signature.Provider;

public interface ISignatureProvider
{
    Task<string> SignTxMsg(string publicKey, string hexMsg);
}

public class SignatureProvider : ISignatureProvider, ISingletonDependency
{
    private const string GetSignatureUri = "/api/app/signature";
    private readonly IHttpProvider _httpProvider;
    private readonly IOptionsMonitor<SignatureServerOptions> _signatureServerOptions;


    public SignatureProvider(IOptionsMonitor<SignatureServerOptions> signatureOptions, IHttpProvider httpProvider)
    {
        _httpProvider = httpProvider;
        _signatureServerOptions = signatureOptions;
    }

    private string Uri(string path) => _signatureServerOptions.CurrentValue.BaseUrl.TrimEnd('/') + path;

    public async Task<string> SignTxMsg(string publicKey, string hexMsg)
    {
        var signatureSend = new SendSignatureDto
        {
            PublicKey = publicKey,
            HexMsg = hexMsg,
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

    public Dictionary<string, string> SecurityServerHeader(params string[] signValues)
    {
        var signString = string.Join(CommonConstant.EmptyString, signValues);
        return new Dictionary<string, string>
        {
            ["appid"] = _signatureServerOptions.CurrentValue.AppId,
            ["signature"] = EncryptionHelper.EncryptHex(signString, _signatureServerOptions.CurrentValue.AppSecret)
        };
    }
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