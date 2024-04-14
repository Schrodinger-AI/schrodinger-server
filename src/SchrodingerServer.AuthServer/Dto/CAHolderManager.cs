using System.Collections.Generic;

namespace SchrodingerServer.Dto;

public class CAHolderManager
{
    public string ChainId { get; set; }
    public string CaHash { get; set; }
    public string CaAddress { get; set; }
    public List<ManagerInfo> managerInfos { get; set; }
}

public class ManagerInfo
{
    public string Address { get; set; }
}