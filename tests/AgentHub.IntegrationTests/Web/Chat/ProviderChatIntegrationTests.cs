using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIAgentHub.Application.Conversations;
using AIAgentHub.Application.Workspaces;
using AIAgentHub.Domain.Workspaces;
using Xunit;

namespace AgentHub.IntegrationTests.Web.Chat;

public sealed class ProviderChatIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CustomWebApplicationFactory _factory;

    public ProviderChatIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.InitializeDatabase();
    }

    [Theory]
    [InlineData("antigravity")]
    [InlineData("claude")]
    [InlineData("codex")]
    [InlineData("gemini")]
    [InlineData("opencode")]
    [InlineData("copilot")]
    public async Task MultiTurnChat_PerProvider_MaintainsSessionContinuity(string providerId)
    {
        var client = await SetupAndAuthenticateClientAsync();

        // 1. Create Workspace
        var tempFolder = Path.Combine(Path.GetTempPath(), "AgentHubTestWS_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        try
        {
            var wsPayload = new CreateWorkspaceRequest(
                Name: $"Test WS {providerId}",
                Path: tempFolder,
                Origin: WorkspaceOrigin.Server,
                DefaultProviderId: providerId,
                DefaultModelId: "default"
            );
            var wsRes = await client.PostAsJsonAsync("/api/v1/workspaces", wsPayload, JsonOpts);
            Assert.Equal(HttpStatusCode.Created, wsRes.StatusCode);
            var workspace = await wsRes.Content.ReadFromJsonAsync<WorkspaceDto>(JsonOpts);
            Assert.NotNull(workspace);

            // 2. Create Conversation
            var convPayload = new CreateConversationRequest(
                WorkspaceId: workspace.Id,
                Title: $"Test Chat {providerId}",
                ProviderId: providerId,
                ModelId: "default"
            );
            var convRes = await client.PostAsJsonAsync("/api/v1/conversations", convPayload, JsonOpts);
            Assert.Equal(HttpStatusCode.Created, convRes.StatusCode);
            var conversation = await convRes.Content.ReadFromJsonAsync<ConversationDto>(JsonOpts);
            Assert.NotNull(conversation);

            // 3. Turn 1 (Message 1)
            var prompt1Res = await client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/prompt", new
            {
                prompt = "Turn 1: Hello from integration test"
            });
            Assert.True(prompt1Res.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK);

            // Wait for background execution to record user + assistant messages
            var state1 = await WaitForMessagesAsync(client, conversation.Id, minimumMessageCount: 2, timeout: TimeSpan.FromSeconds(10));
            Assert.NotNull(state1);
            Assert.True(state1.Messages.Count >= 2, $"Expected at least 2 messages in Turn 1, found {state1.Messages.Count}");

            var turn1AssistantMsg = state1.Messages.FirstOrDefault(m => m.Role == AIAgentHub.Domain.Conversations.MessageRole.Assistant);
            Assert.NotNull(turn1AssistantMsg);
            var session1 = turn1AssistantMsg.Metadata?.ProviderSessionId;

            // 4. Turn 2 (Message 2 - Follow Up)
            var prompt2Res = await client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/prompt", new
            {
                prompt = "Turn 2: Follow up question to verify continuous session"
            });
            Assert.True(prompt2Res.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK);

            // Wait for turn 2 to record user + assistant messages (minimum 4 messages total)
            var state2 = await WaitForMessagesAsync(client, conversation.Id, minimumMessageCount: 4, timeout: TimeSpan.FromSeconds(10));
            Assert.NotNull(state2);
            Assert.True(state2.Messages.Count >= 4, $"Expected at least 4 messages after Turn 2, found {state2.Messages.Count}");

            var turn2AssistantMsg = state2.Messages.LastOrDefault(m => m.Role == AIAgentHub.Domain.Conversations.MessageRole.Assistant);
            Assert.NotNull(turn2AssistantMsg);

            // Session ID consistency assertion
            if (!string.IsNullOrEmpty(session1))
            {
                Assert.Equal(session1, turn2AssistantMsg.Metadata?.ProviderSessionId);
            }
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                try { Directory.Delete(tempFolder, true); } catch { }
            }
        }
    }

    [Theory]
    [InlineData("opencode")]
    [InlineData("codex")]
    [InlineData("copilot")]
    [InlineData("claude")]
    public async Task SwitchProvider_FromAntigravityToEveryOtherProvider_TransfersContextAndSucceeds(string targetProviderId)
    {
        var client = await SetupAndAuthenticateClientAsync();

        // 1. Create Workspace
        var tempFolder = Path.Combine(Path.GetTempPath(), $"AgentHubSwitchWS_{targetProviderId}_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        try
        {
            var wsPayload = new CreateWorkspaceRequest(
                Name: $"Switch WS {targetProviderId}",
                Path: tempFolder,
                Origin: WorkspaceOrigin.Server,
                DefaultProviderId: "antigravity",
                DefaultModelId: "default"
            );
            var wsRes = await client.PostAsJsonAsync("/api/v1/workspaces", wsPayload, JsonOpts);
            Assert.Equal(HttpStatusCode.Created, wsRes.StatusCode);
            var workspace = await wsRes.Content.ReadFromJsonAsync<WorkspaceDto>(JsonOpts);
            Assert.NotNull(workspace);

            // 2. Create Conversation with Antigravity
            var convPayload = new CreateConversationRequest(
                WorkspaceId: workspace.Id,
                Title: $"Switch Chat Antigravity to {targetProviderId}",
                ProviderId: "antigravity",
                ModelId: "default"
            );
            var convRes = await client.PostAsJsonAsync("/api/v1/conversations", convPayload, JsonOpts);
            Assert.Equal(HttpStatusCode.Created, convRes.StatusCode);
            var conversation = await convRes.Content.ReadFromJsonAsync<ConversationDto>(JsonOpts);
            Assert.NotNull(conversation);

            // 3. Turn 1 (Antigravity): Seed prompt with secret number
            var prompt1Res = await client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/prompt", new
            {
                prompt = "remember the number 4951"
            });
            Assert.True(prompt1Res.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK);

            var state1 = await WaitForMessagesAsync(client, conversation.Id, minimumMessageCount: 2, timeout: TimeSpan.FromSeconds(10));
            Assert.NotNull(state1);
            Assert.True(state1.Messages.Count >= 2);
            Assert.Equal("antigravity", state1.ProviderId);

            // 4. Switch Provider from Antigravity to targetProviderId
            var switchPayload = new SwitchProviderRequest(
                TargetProviderId: targetProviderId,
                TargetModelId: "default",
                HistoryScope: "all",
                IncludeFileChanges: true
            );
            var switchRes = await client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/switch-provider", switchPayload, JsonOpts);
            Assert.Equal(HttpStatusCode.OK, switchRes.StatusCode);
            var switchResult = await switchRes.Content.ReadFromJsonAsync<SwitchProviderResult>(JsonOpts);
            Assert.NotNull(switchResult);
            Assert.Equal(targetProviderId, switchResult.ActiveProviderId);
            Assert.Equal(1, switchResult.MigratedMessageCount); // 1 interaction turn (prompt + response)

            // 5. Verify conversation state updated
            var stateAfterSwitch = await client.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}", JsonOpts);
            Assert.NotNull(stateAfterSwitch);
            Assert.Equal(targetProviderId, stateAfterSwitch.ProviderId);

            // 6. Turn 2 (Target Provider): Ask follow-up question
            var prompt2Res = await client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/prompt", new
            {
                prompt = "what was the number I asked you to remember?"
            });
            Assert.True(prompt2Res.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK);

            // Wait for Turn 2 to complete (minimum 4 messages)
            var state2 = await WaitForMessagesAsync(client, conversation.Id, minimumMessageCount: 4, timeout: TimeSpan.FromSeconds(10));
            Assert.NotNull(state2);
            Assert.True(state2.Messages.Count >= 4);

            var lastMsg = state2.Messages.LastOrDefault(m => m.Role == AIAgentHub.Domain.Conversations.MessageRole.Assistant);
            Assert.NotNull(lastMsg);
            Assert.Equal(targetProviderId, lastMsg.OriginProviderId);

            // 7. Verify sessions tracking
            var sessionsRes = await client.GetAsync($"/api/v1/conversations/{conversation.Id}/sessions");
            Assert.Equal(HttpStatusCode.OK, sessionsRes.StatusCode);
            var sessions = await sessionsRes.Content.ReadFromJsonAsync<List<ConversationProviderSessionDto>>(JsonOpts);
            Assert.NotNull(sessions);
            Assert.Contains(sessions, s => s.ProviderId.Equals("antigravity", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(sessions, s => s.ProviderId.Equals(targetProviderId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                try { Directory.Delete(tempFolder, true); } catch { }
            }
        }
    }

    [Theory]
    [InlineData("opencode")]
    [InlineData("codex")]
    [InlineData("copilot")]
    [InlineData("claude")]
    public async Task SwitchProvider_Bidirectional_AntigravityToTargetAndBackToAntigravity_MaintainsContext(string otherProviderId)
    {
        var client = await SetupAndAuthenticateClientAsync();

        var tempFolder = Path.Combine(Path.GetTempPath(), $"AgentHubBidirectionalWS_{otherProviderId}_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        try
        {
            var wsPayload = new CreateWorkspaceRequest(
                Name: $"Bidirectional WS {otherProviderId}",
                Path: tempFolder,
                Origin: WorkspaceOrigin.Server,
                DefaultProviderId: "antigravity",
                DefaultModelId: "default"
            );
            var wsRes = await client.PostAsJsonAsync("/api/v1/workspaces", wsPayload, JsonOpts);
            Assert.Equal(HttpStatusCode.Created, wsRes.StatusCode);
            var workspace = await wsRes.Content.ReadFromJsonAsync<WorkspaceDto>(JsonOpts);
            Assert.NotNull(workspace);

            // 1. Create Conversation on Antigravity
            var convPayload = new CreateConversationRequest(
                WorkspaceId: workspace.Id,
                Title: $"Bidirectional Test {otherProviderId}",
                ProviderId: "antigravity",
                ModelId: "default"
            );
            var convRes = await client.PostAsJsonAsync("/api/v1/conversations", convPayload, JsonOpts);
            var conversation = await convRes.Content.ReadFromJsonAsync<ConversationDto>(JsonOpts);
            Assert.NotNull(conversation);

            // 2. Turn 1 (Antigravity): Seed prompt
            _ = await client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/prompt", new { prompt = "Remember secret number 4951." });
            var state1 = await WaitForMessagesAsync(client, conversation.Id, minimumMessageCount: 2, timeout: TimeSpan.FromSeconds(10));
            Assert.NotNull(state1);
            Assert.True(state1.Messages.Count >= 2);

            // 3. Switch: Antigravity -> otherProviderId
            var switch1Payload = new SwitchProviderRequest(otherProviderId, "default", "all", true);
            var switch1Res = await client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/switch-provider", switch1Payload, JsonOpts);
            Assert.Equal(HttpStatusCode.OK, switch1Res.StatusCode);

            // 4. Turn 2 (otherProviderId): Prompt and advance
            _ = await client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/prompt", new { prompt = "Now also remember the codeword ZEBRA." });
            var state2 = await WaitForMessagesAsync(client, conversation.Id, minimumMessageCount: 4, timeout: TimeSpan.FromSeconds(10));
            Assert.NotNull(state2);
            Assert.True(state2.Messages.Count >= 4);
            Assert.Equal(otherProviderId, state2.Messages.Last().OriginProviderId);

            // 5. Switch BACK: otherProviderId -> Antigravity (vice versa) using delta
            var switchBackPayload = new SwitchProviderRequest("antigravity", "default", "delta", true);
            var switchBackRes = await client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/switch-provider", switchBackPayload, JsonOpts);
            Assert.Equal(HttpStatusCode.OK, switchBackRes.StatusCode);
            var switchBackResult = await switchBackRes.Content.ReadFromJsonAsync<SwitchProviderResult>(JsonOpts);
            Assert.NotNull(switchBackResult);
            Assert.Equal("antigravity", switchBackResult.ActiveProviderId);
            Assert.Equal(1, switchBackResult.MigratedMessageCount); // 1 unshared interaction turn (prompt + response) that ran on otherProviderId

            // 6. Turn 3 (Antigravity): Prompt on returned provider
            _ = await client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/prompt", new { prompt = "What were both the secret number and the codeword?" });
            var state3 = await WaitForMessagesAsync(client, conversation.Id, minimumMessageCount: 6, timeout: TimeSpan.FromSeconds(10));
            Assert.NotNull(state3);
            Assert.True(state3.Messages.Count >= 6);

            var lastMsg = state3.Messages.Last();
            Assert.Equal("antigravity", lastMsg.OriginProviderId);

            // 7. Verify both sessions exist and are updated
            var sessionsRes = await client.GetAsync($"/api/v1/conversations/{conversation.Id}/sessions");
            var sessions = await sessionsRes.Content.ReadFromJsonAsync<List<ConversationProviderSessionDto>>(JsonOpts);
            Assert.NotNull(sessions);
            Assert.Equal(2, sessions.Count);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                try { Directory.Delete(tempFolder, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task SwitchProvider_ToDiscontinuedOrHiddenProvider_ReturnsBadRequest()
    {
        var client = await SetupAndAuthenticateClientAsync();

        var tempFolder = Path.Combine(Path.GetTempPath(), "AgentHubInvalidSwitchWS_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        try
        {
            var wsPayload = new CreateWorkspaceRequest(
                Name: "Invalid WS",
                Path: tempFolder,
                Origin: WorkspaceOrigin.Server,
                DefaultProviderId: "antigravity",
                DefaultModelId: "default"
            );
            var wsRes = await client.PostAsJsonAsync("/api/v1/workspaces", wsPayload, JsonOpts);
            Assert.Equal(HttpStatusCode.Created, wsRes.StatusCode);
            var ws = await wsRes.Content.ReadFromJsonAsync<WorkspaceDto>(JsonOpts);
            Assert.NotNull(ws);

            var convPayload = new CreateConversationRequest(
                WorkspaceId: ws.Id,
                Title: "Conv 1",
                ProviderId: "antigravity",
                ModelId: "default"
            );
            var convRes = await client.PostAsJsonAsync("/api/v1/conversations", convPayload, JsonOpts);
            Assert.Equal(HttpStatusCode.Created, convRes.StatusCode);
            var conv = await convRes.Content.ReadFromJsonAsync<ConversationDto>(JsonOpts);
            Assert.NotNull(conv);

            // Attempt to switch to Gemini (Discontinued)
            var switchPayload = new SwitchProviderRequest("gemini", null, "all");
            var switchRes = await client.PostAsJsonAsync($"/api/v1/conversations/{conv.Id}/switch-provider", switchPayload, JsonOpts);
            Assert.Equal(HttpStatusCode.BadRequest, switchRes.StatusCode);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                try { Directory.Delete(tempFolder, true); } catch { }
            }
        }
    }

    private async Task<ConversationDetailDto?> WaitForMessagesAsync(HttpClient client, Guid conversationId, int minimumMessageCount, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var res = await client.GetAsync($"/api/v1/conversations/{conversationId}");
            if (res.IsSuccessStatusCode)
            {
                var dto = await res.Content.ReadFromJsonAsync<ConversationDetailDto>(JsonOpts);
                if (dto != null && dto.Messages.Count >= minimumMessageCount)
                {
                    return dto;
                }
            }
            await Task.Delay(100);
        }

        // Final attempt
        var finalRes = await client.GetAsync($"/api/v1/conversations/{conversationId}");
        return finalRes.IsSuccessStatusCode ? await finalRes.Content.ReadFromJsonAsync<ConversationDetailDto>(JsonOpts) : null;
    }

    private async Task<HttpClient> SetupAndAuthenticateClientAsync()
    {
        var client = _factory.CreateClient();
        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "admin",
            password = "123456"
        });
        if (!loginRes.IsSuccessStatusCode)
        {
            _ = await client.PostAsJsonAsync("/api/v1/auth/setup/initialize", new
            {
                username = "admin",
                password = "123456",
                confirmPassword = "123456"
            });
            _ = await client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                username = "admin",
                password = "123456"
            });
        }
        return client;
    }
}
