using System.Collections.Generic;

namespace SchrodingerServer.Config;

public interface IConfigAppService
{
    Dictionary<string, string> GetConfig();
}