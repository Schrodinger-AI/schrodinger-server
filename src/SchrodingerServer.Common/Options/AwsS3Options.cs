namespace SchrodingerServer.Common.Options;

public class AwsS3Option
{
    public string AccessKeyID { get; set; }
    public string SecretKey { get; set; }
    public string BucketName { get; set; }
    public string S3Key { get; set; }
    
    public string S3KeySchrodinger { get; set; }
    
    public string ServiceURL { get; set; }
}