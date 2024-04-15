using System;
using System.Collections.Generic;

namespace SchrodingerServer.Users;

public class UserGrainDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string UserName { get; set; }
    public string AelfAddress { get; set; }
    public string CaHash { get; set; }
    public string CaAddressMain { get; set; }
    
    // chainId => address
    public Dictionary<string, string> CaAddressSide { get; set; }
    public string Email { get; set; }

    public string Twitter { get; set; }
    public string Instagram { get; set; }
    public string ProfileImage { get; set; }
    public string ProfileImageOriginal { get; set; }
    public string BannerImage { get; set; }
    
    public string? RegisterDomain { get; set; }

    public long PointRegisterTime { get; set; } = 0;
    public long CreateTime { get; set; }
    public long UpdateTime { get; set; }
}