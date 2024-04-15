using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchrodingerServer.Common.Options;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.AwsS3;

public class AwsS3Client : ISingletonDependency
{
    private const string HttpSchema = "https";
    private const string HostS3 = ".s3.amazonaws.com";
    private readonly AwsS3Option _awsS3Option;

    private AmazonS3Client _amazonS3Client;
    private readonly ILogger<AwsS3Client> _logger;

    public AwsS3Client(IOptionsSnapshot<AwsS3Option> awsS3Option, ILogger<AwsS3Client> logger)
    {
        _logger = logger;
        _awsS3Option = awsS3Option.Value;
        InitAmazonS3Client();
    }

    private void InitAmazonS3Client()
    {
        var accessKeyID = _awsS3Option.AccessKeyID;
        var secretKey = _awsS3Option.SecretKey;
        var ServiceURL = _awsS3Option.ServiceURL;
        var config = new AmazonS3Config()
        {
            ServiceURL = ServiceURL,
            RegionEndpoint = Amazon.RegionEndpoint.APNortheast1
        };
        _amazonS3Client = new AmazonS3Client(accessKeyID, secretKey, config);
    }

    public async Task<string> UpLoadFileForNFTAsync(Stream steam, string fileName)
    {
        var putObjectRequest = new PutObjectRequest
        {
            InputStream = steam,
            BucketName = _awsS3Option.BucketName,
            Key = _awsS3Option.S3KeySchrodinger + "/" + fileName,
            CannedACL = S3CannedACL.PublicRead,
        };
        var start = DateTime.Now;
        var putObjectResponse = await _amazonS3Client.PutObjectAsync(putObjectRequest);
        var timeCost = (DateTime.Now - start).TotalMilliseconds;
        _logger.LogInformation("UpLoadFileForNFTAsync cost time: {timeCost}ms", timeCost);
        UriBuilder uriBuilder = new UriBuilder
        {
            Scheme = HttpSchema,
            Host = _awsS3Option.BucketName + HostS3,
            Path = "/" + _awsS3Option.S3KeySchrodinger + "/" + fileName
        };

        return putObjectResponse.HttpStatusCode == HttpStatusCode.OK
            ? uriBuilder.ToString()
            : string.Empty;
    }

    public async Task<GetObjectResponse> GetObjectAsync(string keyName)
    {
        var getRequest = new GetObjectRequest
        {
            BucketName = _awsS3Option.BucketName,
            Key = keyName
        };
        return await _amazonS3Client.GetObjectAsync(getRequest);
    }
}