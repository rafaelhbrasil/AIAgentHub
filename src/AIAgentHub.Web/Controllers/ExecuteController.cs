using AIAgentHub.Application.Execution;

using Microsoft.AspNetCore.Mvc;

namespace AIAgentHub.Web.Controllers;

[ApiController]
[Route("api/v1/conversations/{id:guid}")]
public sealed class ExecuteController(IServiceScopeFactory scopeFactory, ILogger<ExecuteController> logger) : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<ExecuteController> _logger = logger;

    public sealed record PromptRequest(string Prompt);

    [HttpPost("prompt")]
    public IActionResult ExecutePrompt(Guid id, [FromBody] PromptRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { code = "empty_prompt", message = "Prompt cannot be empty." });
        }

        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IExecutionOrchestrator>();
            try
            {
                await orchestrator.ExecuteAsync(id, request.Prompt, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background AI execution failed for conversation {ConversationId}", id);
            }
        });

        return Accepted(new { status = "started", conversationId = id });
    }

    [HttpPost("abort")]
    public async Task<IActionResult> AbortExecution(Guid id)
    {
        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IExecutionOrchestrator>();
        await orchestrator.AbortAsync(id);
        return Ok(new { status = "aborted", conversationId = id });
    }
}
