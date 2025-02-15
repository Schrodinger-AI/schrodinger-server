using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;

namespace SchrodingerServer.Middleware;

public class DeviceInfoMiddleware
{
    private readonly ILogger<DeviceInfoMiddleware> _logger;
    private readonly IOptionsMonitor<AccessVerifyOptions> _ipWhiteListOptions;
    private readonly RequestDelegate _next;

    public DeviceInfoMiddleware(RequestDelegate next, ILogger<DeviceInfoMiddleware> logger,
        IOptionsMonitor<AccessVerifyOptions> ipWhiteListOptions)
    {
        _next = next;
        _logger = logger;
        _ipWhiteListOptions = ipWhiteListOptions;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        DeviceInfoContext.CurrentDeviceInfo = ExtractDeviceInfo(context);
        try
        {
            await _next(context);
        }
        finally
        {
            DeviceInfoContext.Clear();
        }
    }

    private DeviceInfo ExtractDeviceInfo(HttpContext context)
    {
        var headers = context.Request.Headers;
        var clientTypeExists = headers.TryGetValue("Client-Type", out var clientType);
        var clientVersionExists = headers.TryGetValue("Version", out var clientVersion);
        var hostHeader = _ipWhiteListOptions.CurrentValue.HostHeader ?? "Host";
    
        return new DeviceInfo
        {
            ClientType = clientTypeExists ? clientType.ToString() : null,
            Version = clientVersionExists ? clientVersion.ToString() : null,
            ClientIp = GetClientIp(context),
            Host = headers[hostHeader].FirstOrDefault() ?? CommonConstant.EmptyString
        };
    }
    
    private string GetClientIp(HttpContext context)
    {
        // Check the X-Forwarded-For header (set by some agents)
        var forwardedHeader = context.Request.Headers["X-Forwarded-For"];
        if (!string.IsNullOrEmpty(forwardedHeader))
        {
            var ip = forwardedHeader.FirstOrDefault();
            if (!string.IsNullOrEmpty(ip))
            {
                return ip.Split(',')[0].Trim(); // Take the first IP (if there are more than one)
            }
        }
    
        // Check the X-Real-IP header (set by some agents)
        var realIpHeader = context.Request.Headers["X-Real-IP"];
        if (!string.IsNullOrEmpty(realIpHeader))
        {
            return realIpHeader;
        }
    
        var ipAddress = context.Connection.RemoteIpAddress;
    
        // Use remote IP address as fallback
        return ipAddress?.IsIPv4MappedToIPv6 ?? false ? ipAddress.MapToIPv4().ToString() : ipAddress?.ToString();
    }
}