using System.Collections.Generic;

namespace SchrodingerServer.AddressRelationship.Dto;

public class RemainPointDto
{
    public List<UnboundEvmAddressPoints> RemainPointList { get; set; }
}

public class UnboundEvmAddressPoints
{
    public string Address { get; set; }
    public string Points { get; set; }
}