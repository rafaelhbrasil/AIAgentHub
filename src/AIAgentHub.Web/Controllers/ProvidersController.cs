using AIAgentHub.Application.Providers;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentHub.Web.Controllers;

[ApiController]
[Route("api/v1/providers")]
public sealed class ProvidersController : ControllerBase
{
    private readonly IProviderManager _providerManager;

    public ProvidersController(IProviderManager providerManager)
    {
        _providerManager = providerManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var providers = await _providerManager.DetectAllAsync(cancellationToken);
        return Ok(providers);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var info = await _providerManager.GetProviderInfoAsync(id, cancellationToken);
        if (info == null)
            return NotFound(new { code = "provider_not_found", message = $"Provider '{id}' was not found." });
        return Ok(info);
    }

    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetDetailedStatus(string id, [FromQuery] bool refresh = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _providerManager.DetectProviderDetailedAsync(id, refresh, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { code = "provider_not_found", message = $"Provider '{id}' was not found." });
        }
    }

    [HttpGet("{id}/models")]
    public async Task<IActionResult> GetModels(string id, [FromQuery] bool refresh = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await _providerManager.GetModelsAsync(id, refresh, cancellationToken);
            return Ok(models);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { code = "provider_not_found", message = $"Provider '{id}' was not found." });
        }
    }

    [HttpPost("{id}/authenticate")]
    public async Task<IActionResult> Authenticate(string id, CancellationToken cancellationToken)
    {
        try
        {
            var provider = _providerManager.GetProvider(id);
            var result = await provider.LaunchAuthenticationAsync(cancellationToken);
            return Ok(new { success = true, message = result });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { code = "provider_not_found", message = $"Provider '{id}' was not found." });
        }
    }

    public sealed record UpdateModelSettingsRequest(Dictionary<string, bool> ModelStates);

    [HttpPut("{id}/models/settings")]
    public async Task<IActionResult> UpdateModelSettings(string id, [FromBody] UpdateModelSettingsRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            await _providerManager.UpdateModelSettingsAsync(id, request.ModelStates ?? new(), cancellationToken);
            return Ok(new { success = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { code = "provider_not_found", message = $"Provider '{id}' was not found." });
        }
    }
}
