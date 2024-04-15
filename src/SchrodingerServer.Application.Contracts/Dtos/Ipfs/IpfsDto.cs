using System;
using System.Runtime.InteropServices.JavaScript;

namespace SchrodingerServer.Dtos.Ipfs;

public class IpfsBody
{
    public PinataContent pinataContent{ get; set; }
    public PinataMetadata pinataMetadata { get; set; }
    public PinataOptions pinataOptions { get; set; }
}

public class PinataContent
{
    public string data { get; set; }
}

public class PinataMetadata
{
    public string name { get; set; }
}


public class PinataOptions
{
    public int cidVersion { get; set; }
}

public class IpfsResponse
{
    public string IpfsHash{ get; set; }
    public long PinSize { get; set; }
    public string Timestamp { get; set; }
}