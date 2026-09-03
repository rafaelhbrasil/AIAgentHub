using System.Reflection;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace AIAgentHub.Web.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/system")]
public sealed class SystemController(IWebHostEnvironment environment) : ControllerBase
{
    private readonly IWebHostEnvironment _environment = environment;

    public sealed record SystemVersionResponse(
        string Version,
        string InformationalVersion,
        bool IsDevelopment,
        string Environment);

    [HttpGet("version")]
    public IActionResult GetVersion()
    {
        var asm = typeof(Program).Assembly;
        var infoVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var fileVersion = asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        var asmVersion = asm.GetName().Version?.ToString();
        var isDevelopment = _environment.IsDevelopment();

        var cleanInfoVersion = infoVersion?.Split('+')[0];
        var rawVersion = !string.IsNullOrWhiteSpace(cleanInfoVersion)
            ? cleanInfoVersion
            : fileVersion ?? asmVersion ?? "0.1.0";
        var version = FormatDisplayVersion(rawVersion);

        return Ok(new SystemVersionResponse(
            Version: version,
            InformationalVersion: infoVersion ?? version,
            IsDevelopment: isDevelopment,
            Environment: _environment.EnvironmentName));
    }

    public static string FormatDisplayVersion(string rawVersion)
    {
        if (System.Version.TryParse(rawVersion, out var parsed) && parsed.Revision == 0)
        {
            return $"{parsed.Major}.{parsed.Minor}.{parsed.Build}";
        }

        return rawVersion;
    }
}
