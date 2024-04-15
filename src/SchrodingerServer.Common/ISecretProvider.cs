using AElf.Types;

namespace SchrodingerServer.Common;

public interface ISecretProvider
{
    Task<string> GetSignatureAsync(string publicKey, Transaction transaction);
    Task<string> GetSecretWithCacheAsync(string key);
    Task<string> GetSignatureFromHashAsync(string publicKey, Hash hash);
}