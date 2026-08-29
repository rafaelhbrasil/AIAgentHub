using AIAgentHub.Application.Conversations;
using AIAgentHub.Domain.Configuration;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AIAgentHub.Web.Controllers;

[ApiController]
[Route("api/v1/conversations")]
public sealed class ConversationsController(
    IConversationService conversationService,
    IConversationSwitchService conversationSwitchService,
    IOptions<ProviderSwitchOptions>? switchOptions = null) : ApiControllerBase
{
    private readonly IConversationService _conversationService = conversationService;
    private readonly IConversationSwitchService _conversationSwitchService = conversationSwitchService;
    private readonly IOptions<ProviderSwitchOptions>? _switchOptions = switchOptions;

    [HttpGet("switch-config")]
    public IActionResult GetSwitchConfig()
    {
        var options = _switchOptions?.Value ?? new ProviderSwitchOptions();
        return Ok(new
        {
            recentMessageCounts = options.RecentMessageCounts
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetByWorkspace([FromQuery] Guid workspaceId, CancellationToken cancellationToken)
    {
        var list = await _conversationService.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var conv = await _conversationService.GetByIdAsync(id, cancellationToken);
        return conv == null ? NotFoundResponse("conversation_not_found", $"Conversation {id} was not found.") : Ok(conv);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var created = await _conversationService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    public sealed record RenameConversationRequest(string Title);
    public sealed record UpdateConversationModelRequest(string? ModelId, string? ProviderId = null, string? Effort = null);
    public sealed record SetConversationPinRequest(bool IsPinned);

    [HttpPut("{id:guid}/rename")]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameConversationRequest request, CancellationToken cancellationToken)
    {
        var updated = await _conversationService.RenameAsync(id, request.Title, cancellationToken);
        return Ok(updated);
    }

    [HttpPut("{id:guid}/pin")]
    public async Task<IActionResult> SetPin(Guid id, [FromBody] SetConversationPinRequest request, CancellationToken cancellationToken)
    {
        var updated = await _conversationService.SetPinnedAsync(id, request.IsPinned, cancellationToken);
        return Ok(updated);
    }

    [HttpPut("{id:guid}/model")]
    public async Task<IActionResult> UpdateModel(Guid id, [FromBody] UpdateConversationModelRequest request, CancellationToken cancellationToken)
    {
        var conv = await _conversationService.GetByIdAsync(id, cancellationToken);
        if (conv == null)
        {
            return NotFoundResponse("conversation_not_found", $"Conversation {id} was not found.");
        }

        var providerId = string.IsNullOrWhiteSpace(request.ProviderId) ? conv.ProviderId : request.ProviderId;
        await _conversationService.SetProviderAndModelAsync(id, providerId, request.ModelId, request.Effort, cancellationToken);
        var updated = await _conversationService.GetByIdAsync(id, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:guid}/switch-provider")]
    public async Task<IActionResult> SwitchProvider(Guid id, [FromBody] SwitchProviderRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _conversationSwitchService.SwitchProviderAsync(id, request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse("not_found", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse("invalid_provider", ex.Message);
        }
    }

    [HttpPost("{id:guid}/abort-switch")]
    public async Task<IActionResult> AbortSwitch(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _conversationSwitchService.AbortSwitchAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse("not_found", ex.Message);
        }
    }

    [HttpGet("{id:guid}/sessions")]
    public async Task<IActionResult> GetSessions(Guid id, CancellationToken cancellationToken)
    {
        var sessions = await _conversationSwitchService.GetSessionsAsync(id, cancellationToken);
        return Ok(sessions);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Patch(Guid id, [FromBody] UpdateConversationModelRequest request, CancellationToken cancellationToken) => await UpdateModel(id, request, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _conversationService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken cancellationToken)
    {
        var results = await _conversationService.SearchAsync(q, cancellationToken);
        return Ok(results);
    }
}
