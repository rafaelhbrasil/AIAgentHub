using System.Net.NetworkInformation;

using AIAgentHub.Application.Execution;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Security;

using Microsoft.AspNetCore.Mvc;

using IAppAuthService = AIAgentHub.Application.Security.IAuthenticationService;

namespace AIAgentHub.Web.Controllers;

[ApiController]
[Route("api/v1/permissions")]
public sealed class PermissionsController(IPermissionService permissionService) : ControllerBase
{
    private readonly IPermissionService _permissionService = permissionService;

    [HttpGet]
    public async Task<IActionResult> GetByConversation([FromQuery] Guid conversationId, CancellationToken cancellationToken)
    {
        var requests = await _permissionService.GetRequestsByConversationAsync(conversationId, cancellationToken);
        return Ok(requests);
    }

    public sealed record DecidePermissionRequest(bool Approve);

    [HttpPost("{id:guid}/decide")]
    public async Task<IActionResult> Decide(Guid id, [FromBody] DecidePermissionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _permissionService.DecideAsync(id, request.Approve, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { code = "permission_request_not_found", message = $"Permission request {id} was not found." });
        }
    }
}

[ApiController]
[Route("api/v1/settings")]
public sealed class SettingsController(IServerSettingsRepository settingsRepository, IAppAuthService authService) : ControllerBase
{
    private readonly IServerSettingsRepository _settingsRepository = settingsRepository;
    private readonly IAppAuthService _authService = authService;

    public sealed record UpdateServerSettingsRequest(
        NetworkMode NetworkMode,
        int? ListeningPortHttps = null,
        int? ListeningPortHttp = null,
        List<string>? SelectedInterfaces = null,
        string? Theme = null);

    [HttpGet]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await _settingsRepository.GetAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateSettings(Guid id, [FromBody] UpdateServerSettingsRequest request, CancellationToken cancellationToken)
    {
        var existing = await _settingsRepository.GetAsync(cancellationToken);
        if (existing.Id != id)
        {
            return NotFound(new { code = "settings_not_found", message = $"Server settings with ID '{id}' was not found." });
        }

        existing.NetworkMode = request.NetworkMode;
        if (request.ListeningPortHttps.HasValue)
        {
            existing.ListeningPortHttps = request.ListeningPortHttps.Value;
        }

        if (request.ListeningPortHttp.HasValue)
        {
            existing.ListeningPortHttp = request.ListeningPortHttp.Value;
        }

        if (request.SelectedInterfaces != null)
        {
            existing.SelectedInterfaces = request.SelectedInterfaces;
        }

        if (!string.IsNullOrWhiteSpace(request.Theme))
        {
            existing.Theme = request.Theme;
        }

        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _settingsRepository.UpdateAsync(existing, cancellationToken);
        return Ok(existing);
    }

    [HttpGet("network-interfaces")]
    public IActionResult GetNetworkInterfaces()
    {
        var list = new List<object>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            var props = nic.GetIPProperties();
            foreach (var addr in props.UnicastAddresses)
            {
                if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    list.Add(new
                    {
                        name = nic.Name,
                        description = nic.Description,
                        ipAddress = addr.Address.ToString(),
                        status = nic.OperationalStatus.ToString()
                    });
                }
            }
        }
        return Ok(list);
    }

    [HttpGet("recovery-code")]
    public async Task<IActionResult> GetRecoveryCode(CancellationToken cancellationToken)
    {
        var admin = await _authService.GetAdminAsync(cancellationToken);
        return admin == null
            ? NotFound()
            : Ok(new
            {
                username = admin.Username,
                hasRecoveryCode = !string.IsNullOrEmpty(admin.RecoveryCodeHash)
            });
    }
}

[ApiController]
[Route("api/v1/skills")]
public sealed class SkillsController(ISkillRepository skillRepository) : ControllerBase
{
    private readonly ISkillRepository _skillRepository = skillRepository;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var skills = await _skillRepository.GetAllAsync(cancellationToken);
        return Ok(skills);
    }
}

[ApiController]
[Route("api/v1/mcps")]
public sealed class McpsController(IMcpServerRepository mcpRepository) : ControllerBase
{
    private readonly IMcpServerRepository _mcpRepository = mcpRepository;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var mcps = await _mcpRepository.GetAllAsync(cancellationToken);
        return Ok(mcps);
    }
}
