using AIAgentHub.Application.Conversations;
using AIAgentHub.Domain.Conversations;
using AIAgentHub.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace AgentHub.UnitTests.Controllers;

public class ConversationsControllerTests
{
    private readonly IConversationService _conversationService = Substitute.For<IConversationService>();
    private readonly IConversationSwitchService _conversationSwitchService = Substitute.For<IConversationSwitchService>();
    private readonly ConversationsController _controller;

    public ConversationsControllerTests()
    {
        _controller = new ConversationsController(_conversationService, _conversationSwitchService, Microsoft.Extensions.Options.Options.Create(new AIAgentHub.Domain.Configuration.ProviderSwitchOptions()));
    }

    [Fact]
    public async Task SwitchProvider_CallsSwitchService_ReturnsOk()
    {
        var convId = Guid.NewGuid();
        var request = new SwitchProviderRequest("claude-code", "claude-3-7-sonnet", "all");
        var expectedResult = new SwitchProviderResult(convId, "claude-code", "claude-3-7-sonnet", 5, "sess-1");

        _conversationSwitchService.SwitchProviderAsync(convId, request, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var actionResult = await _controller.SwitchProvider(convId, request, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(expectedResult, okResult.Value);
    }

    [Fact]
    public async Task GetSessions_CallsSwitchService_ReturnsOk()
    {
        var convId = Guid.NewGuid();
        var sessions = new List<ConversationProviderSessionDto>
        {
            new(Guid.NewGuid(), convId, "gemini", "sess-1", null, 2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        };

        _conversationSwitchService.GetSessionsAsync(convId, Arg.Any<CancellationToken>())
            .Returns(sessions);

        var actionResult = await _controller.GetSessions(convId, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(sessions, okResult.Value);
    }

    [Fact]
    public async Task SetPin_CallsConversationService_ReturnsOk()
    {
        var convId = Guid.NewGuid();
        var dto = new ConversationDto(convId, Guid.NewGuid(), "Pinned Conv", "gemini", null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0, 0, null, ConversationStatus.Active, true);

        _conversationService.SetPinnedAsync(convId, true, Arg.Any<CancellationToken>())
            .Returns(dto);

        var actionResult = await _controller.SetPin(convId, new ConversationsController.SetConversationPinRequest(true), CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(dto, okResult.Value);
    }

    [Fact]
    public async Task SwitchProvider_InvalidOperation_ReturnsBadRequest()
    {
        var convId = Guid.NewGuid();
        var request = new SwitchProviderRequest("discontinued-prov", null, "all");

        _conversationSwitchService.SwitchProviderAsync(convId, request, Arg.Any<CancellationToken>())
            .Returns<SwitchProviderResult>(_ => throw new InvalidOperationException("Provider discontinued"));

        var actionResult = await _controller.SwitchProvider(convId, request, CancellationToken.None);
        _ = Assert.IsType<BadRequestObjectResult>(actionResult);
    }

    [Fact]
    public async Task SwitchProvider_NotFound_ReturnsNotFound()
    {
        var convId = Guid.NewGuid();
        var request = new SwitchProviderRequest("non-existent", null, "all");

        _conversationSwitchService.SwitchProviderAsync(convId, request, Arg.Any<CancellationToken>())
            .Returns<SwitchProviderResult>(_ => throw new KeyNotFoundException("Conversation not found"));

        var actionResult = await _controller.SwitchProvider(convId, request, CancellationToken.None);
        _ = Assert.IsType<NotFoundObjectResult>(actionResult);
    }
}
