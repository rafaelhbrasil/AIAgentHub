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
        var initRes = await client.PostAsJsonAsync("/api/v1/auth/setup/initialize", new
        {
            username = "admin",
            password = "SecurePassword123!",
            confirmPassword = "SecurePassword123!"
        });
        if (!initRes.IsSuccessStatusCode)
        {
            _ = await client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                username = "admin",
                password = "SecurePassword123!"
            });
        }
        return client;
    }
}
