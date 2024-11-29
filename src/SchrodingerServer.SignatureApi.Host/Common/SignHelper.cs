using System;
using System.Text;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;

namespace SchrodingerServer.SignatureServer.Common;

public static class SignHelper
{
    public static string GetSignature(string appSecret, string input)
    {
        var privateKey = new Key(Encoders.Hex.DecodeData(appSecret));
        var hash = Hashes.DoubleSHA256(input.GetBytes(Encoding.UTF8));
        var signature = privateKey.Sign(hash);
        return  Encoders.Hex.EncodeData(signature.ToDER());
    }
}