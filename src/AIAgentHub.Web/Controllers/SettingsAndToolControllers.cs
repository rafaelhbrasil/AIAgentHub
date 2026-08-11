using System.Net.NetworkInformation;
using AIAgentHub.Application.Execution;
using AIAgentHub.Application.Security;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Security;
using Microsoft.AspNetCore.Mvc;
using IAppAuthService = AIAgentHub.Application.Security.IAuthenticationService;

namespace AIAgentHub.Web.Controllers;

[ApiController]
[Route("api/v1/permissions")]
public sealed class PermissionsController : ControllerBase
{
    private readonly IPermissionService _permissionService;

    public PermissionsController(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

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
public sealed class SettingsController : ControllerBase
{
    private readonly IServerSettingsRepository _settingsRepository;
    private readonly IAppAuthService _authService;

    public SettingsController(IServerSettingsRepository settingsRepository, IAppAuthService authService)
    {
        _settingsRepository = settingsRepository;
        _authService = authService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await _settingsRepository.GetAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateSettings([FromBody] ServerSettings newSettings, CancellationToken cancellationToken)
    {
        await _settingsRepository.UpdateAsync(newSettings, cancellationToken);
        return Ok(newSettings);
    }

    [HttpGet("network-interfaces")]
    public IActionResult GetNetworkInterfaces()
    {
        var list = new List<object>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;

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
        if (admin == null) return NotFound();

        return Ok(new
        {
            username = admin.Username,
            hasRecoveryCode = !string.IsNullOrEmpty(admin.RecoveryCodeHash)
        });
    }
}

[ApiController]
[Route("api/v1/skills")]
public sealed class SkillsController : ControllerBase
{
    private readonly ISkillRepository _skillRepository;

    public SkillsController(ISkillRepository skillRepository)
    {
        _skillRepository = skillRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var skills = await _skillRepository.GetAllAsync(cancellationToken);
        return Ok(skills);
    }
}

[ApiController]
[Route("api/v1/mcps")]
public sealed class McpsController : ControllerBase
{
    private readonly IMcpServerRepository _mcpRepository;

    public McpsController(IMcpServerRepository mcpRepository)
    {
        _mcpRepository = mcpRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var mcps = await _mcpRepository.GetAllAsync(cancellationToken);
        return Ok(mcps);
    }
}
