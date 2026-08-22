using System.Text.Json;
using System.Text.Json.Serialization;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Providers;

using Microsoft.AspNetCore.Mvc;

namespace AIAgentHub.Web.Controllers;

[ApiController]
[Route("api/v1/providers")]
public sealed class ProvidersController(IProviderManager providerManager) : ApiControllerBase
{
    private readonly IProviderManager _providerManager = providerManager;

    [HttpGet("refresh-stream")]
    public async Task RefreshStream(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache, no-transform");
        Response.Headers.Append("Connection", "keep-alive");

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        await foreach (var evt in _providerManager.StreamRefreshAllAsync(cancellationToken))
        {
            var json = JsonSerializer.Serialize((object)evt, evt.GetType(), jsonOptions);
            var message = $"event: {evt.Type}\ndata: {json}\n\n";
            await Response.WriteAsync(message, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool refresh = false, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProviderInfo> providers;

        if (refresh)
        {
            // Force refresh all providers in parallel
            providers = await _providerManager.RefreshAllAsync(cancellationToken);
        }
        else
        {
            // Read from DB cache
            providers = await _providerManager.GetAllAsync(cancellationToken);
        }

        return Ok(providers);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var info = await _providerManager.GetProviderInfoAsync(id, cancellationToken);
        return info == null ? NotFoundResponse("provider_not_found", $"Provider '{id}' was not found.") : Ok(info);
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
            return NotFoundResponse("provider_not_found", $"Provider '{id}' was not found.");
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
            return NotFoundResponse("provider_not_found", $"Provider '{id}' was not found.");
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
            return NotFoundResponse("provider_not_found", $"Provider '{id}' was not found.");
        }
    }

    public sealed record UpdateModelSettingsRequest(Dictionary<string, bool> ModelStates);

    [HttpPut("{id}/models/settings")]
    public async Task<IActionResult> UpdateModelSettings(string id, [FromBody] UpdateModelSettingsRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            await _providerManager.UpdateModelSettingsAsync(id, request.ModelStates ?? [], cancellationToken);
            return Ok(new { success = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFoundResponse("provider_not_found", $"Provider '{id}' was not found.");
        }
    }
}
