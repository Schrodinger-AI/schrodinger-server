using System.Threading.Tasks;

namespace SchrodingerServer.Ipfs;

public interface IIpfsAppService
{
    public Task<string> Upload(string content, string name);
    
    public Task<string> UploadFile(string base64String, string name);
}