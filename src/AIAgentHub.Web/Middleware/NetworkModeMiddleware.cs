using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

using AIAgentHub.Application.Security;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Security;

namespace AIAgentHub.Web.Middleware;

public sealed class NetworkModeMiddleware(RequestDelegate next, RecoveryOptions recoveryOptions)
{
    private readonly RequestDelegate _next = next;
    private readonly RecoveryOptions _recoveryOptions = recoveryOptions;

    public async Task InvokeAsync(HttpContext context, IServerSettingsRepository settingsRepository)
    {
        var remoteIp = context.Connection.RemoteIpAddress;

        // Map IPv4-mapped IPv6 addresses (e.g. ::ffff:192.168.1.5) to standard IPv4
        if (remoteIp != null && remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        // Loopback connections (127.0.0.1, ::1) and Safe Client IP are always allowed regardless of network mode
        if (remoteIp == null || IPAddress.IsLoopback(remoteIp) || _recoveryOptions.IsSafeClient(remoteIp))
        {
            await _next(context);
            return;
        }

        var settings = await settingsRepository.GetAsync(context.RequestAborted);

        if (settings.NetworkMode == NetworkMode.Localhost)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                "{\"code\":\"forbidden\",\"message\":\"Remote access is disabled. Server is configured in Localhost-only mode.\"}",
                context.RequestAborted);
            return;
        }

        if (settings.NetworkMode == NetworkMode.Lan)
        {
            // All local network interfaces / LAN addresses allowed
            await _next(context);
            return;
        }

        if (settings.NetworkMode == NetworkMode.SelectedInterfaces)
        {
            var allowedIps = GetAllowedSelectedIps(settings.SelectedInterfaces);
            var isAllowed = allowedIps.Any(ip => ip.Equals(remoteIp) || IsInSameSubnet(remoteIp, ip));

            if (isAllowed)
            {
                await _next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                "{\"code\":\"forbidden\",\"message\":\"Remote access forbidden. Client IP is not in the allowed network interfaces list.\"}",
                context.RequestAborted);
            return;
        }

        await _next(context);
    }

    private static List<IPAddress> GetAllowedSelectedIps(List<string>? selectedInterfaces)
    {
        var allowed = new List<IPAddress>();
        if (selectedInterfaces == null || selectedInterfaces.Count == 0)
        {
            return allowed;
        }

        foreach (var selected in selectedInterfaces)
        {
            if (IPAddress.TryParse(selected, out var parsedIp))
            {
                allowed.Add(parsedIp);
            }
        }

        // Also resolve matching network interface names
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (selectedInterfaces.Contains(nic.Name, StringComparer.OrdinalIgnoreCase) ||
                selectedInterfaces.Contains(nic.Id, StringComparer.OrdinalIgnoreCase) ||
                selectedInterfaces.Contains(nic.Description, StringComparer.OrdinalIgnoreCase))
            {
                var props = nic.GetIPProperties();
                foreach (var unicast in props.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                    {
                        allowed.Add(unicast.Address);
                    }
                }
            }
        }

        return allowed;
    }

    private static bool IsInSameSubnet(IPAddress clientIp, IPAddress adapterIp)
    {
        // Simple /24 subnet match for IPv4 LAN checking (ponytail: sufficient for LAN discovery)
        if (clientIp.AddressFamily == AddressFamily.InterNetwork && adapterIp.AddressFamily == AddressFamily.InterNetwork)
        {
            var clientBytes = clientIp.GetAddressBytes();
            var adapterBytes = adapterIp.GetAddressBytes();
            return clientBytes[0] == adapterBytes[0] &&
                   clientBytes[1] == adapterBytes[1] &&
                   clientBytes[2] == adapterBytes[2];
        }

        return false;
    }
}
