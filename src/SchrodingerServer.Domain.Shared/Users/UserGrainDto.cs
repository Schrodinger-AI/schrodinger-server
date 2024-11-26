using System;
using System.Collections.Generic;
using Orleans;

namespace SchrodingerServer.Users;

[GenerateSerializer]
public class UserGrainDto
{
    [Id(0)]
    public Guid Id { get; set; }
    [Id(1)]
    public string Name { get; set; }
    [Id(2)]
    public string UserName { get; set; }
    [Id(3)]
    public string AelfAddress { get; set; }
    [Id(4)]
    public string CaHash { get; set; }
    [Id(5)]
    public string CaAddressMain { get; set; }
    
    // chainId => address
    [Id(6)]
    public Dictionary<string, string> CaAddressSide { get; set; }
    [Id(7)]
    public string Email { get; set; }
    [Id(8)]
    public string Twitter { get; set; }
    [Id(9)]
    public string Instagram { get; set; }
    [Id(10)]
    public string ProfileImage { get; set; }
    [Id(11)]
    public string ProfileImageOriginal { get; set; }
    [Id(12)]
    public string BannerImage { get; set; }
    
    [Id(13)]
    public string? RegisterDomain { get; set; }
    [Id(14)]
    public long PointRegisterTime { get; set; } = 0;
    [Id(15)]
    public long CreateTime { get; set; }
    [Id(16)]
    public long UpdateTime { get; set; }
}