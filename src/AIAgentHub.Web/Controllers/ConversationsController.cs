using AIAgentHub.Application.Conversations;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentHub.Web.Controllers;

[ApiController]
[Route("api/v1/conversations")]
public sealed class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversationService;

    public ConversationsController(IConversationService conversationService)
    {
        _conversationService = conversationService;
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
        if (conv == null)
            return NotFound(new { code = "conversation_not_found", message = $"Conversation {id} was not found." });
        return Ok(conv);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var created = await _conversationService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    public sealed record RenameConversationRequest(string Title);
    public sealed record UpdateConversationModelRequest(string? ModelId, string? ProviderId = null, string? Effort = null);

    [HttpPut("{id:guid}/rename")]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameConversationRequest request, CancellationToken cancellationToken)
    {
        var updated = await _conversationService.RenameAsync(id, request.Title, cancellationToken);
        return Ok(updated);
    }

    [HttpPut("{id:guid}/model")]
    public async Task<IActionResult> UpdateModel(Guid id, [FromBody] UpdateConversationModelRequest request, CancellationToken cancellationToken)
    {
        var conv = await _conversationService.GetByIdAsync(id, cancellationToken);
        if (conv == null)
            return NotFound(new { code = "conversation_not_found", message = $"Conversation {id} was not found." });

        var providerId = string.IsNullOrWhiteSpace(request.ProviderId) ? conv.ProviderId : request.ProviderId;
        await _conversationService.SetProviderAndModelAsync(id, providerId, request.ModelId, request.Effort, cancellationToken);
        var updated = await _conversationService.GetByIdAsync(id, cancellationToken);
        return Ok(updated);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Patch(Guid id, [FromBody] UpdateConversationModelRequest request, CancellationToken cancellationToken)
    {
        return await UpdateModel(id, request, cancellationToken);
    }

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
