using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIAgentHub.Application.Conversations;
using AIAgentHub.Application.Providers;
using AIAgentHub.Application.Workspaces;
using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Domain.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentHub.IntegrationTests.Web.Chat;

public sealed class LiveProviderChatIntegrationTests : IClassFixture<LiveProviderWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly LiveProviderWebApplicationFactory _factory;

    public LiveProviderChatIntegrationTests(LiveProviderWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.InitializeDatabaseAndAdminAsync().GetAwaiter().GetResult();
    }

    [SkippableTheory]
    [InlineData("antigravity")]
    [InlineData("claude")]
    [InlineData("codex")]
    [InlineData("gemini")]
    [InlineData("opencode")]
    [InlineData("copilot")]
    public async Task LiveProvider_TwoTurnMemoryRecall_MaintainsSessionContinuity(string providerId)
    {
        // 1. Verify Provider Availability & Dynamic Skip if not installed or unauthenticated
        var provider = await CheckProviderAvailabilityOrSkipAsync(providerId);

        var client = await SetupAndAuthenticateClientAsync();

        // 2. Setup Workspace
        var tempFolder = Path.Combine(Path.GetTempPath(), $"AgentHubLiveWS_{providerId}_" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempFolder);

        var conversationId = Guid.Empty;

        try
        {
            var wsPayload = new CreateWorkspaceRequest(
                Name: $"Live WS {providerId}",
                Path: tempFolder,
                Origin: WorkspaceOrigin.Server,
                DefaultProviderId: providerId,
                DefaultModelId: "default"
            );
            var wsRes = await client.PostAsJsonAsync("/api/v1/workspaces", wsPayload, JsonOpts);
            Assert.Equal(HttpStatusCode.Created, wsRes.StatusCode);
            var workspace = await wsRes.Content.ReadFromJsonAsync<WorkspaceDto>(JsonOpts);
            Assert.NotNull(workspace);

            // 3. Create Conversation
            var convPayload = new CreateConversationRequest(
                WorkspaceId: workspace.Id,
                Title: $"Live Memory Test {providerId}",
                ProviderId: providerId,
                ModelId: "default"
            );
            var convRes = await client.PostAsJsonAsync("/api/v1/conversations", convPayload, JsonOpts);
            Assert.Equal(HttpStatusCode.Created, convRes.StatusCode);
            var conversation = await convRes.Content.ReadFromJsonAsync<ConversationDto>(JsonOpts);
            Assert.NotNull(conversation);
            conversationId = conversation.Id;

            // Generate a random 6-digit secret number
            var secret = Random.Shared.Next(100000, 999999);

            // 4. Turn 1 (Memory Seed Prompt)
            var prompt1 = $"Remember the number {secret}. Reply with just the word 'ACKNOWLEDGED'.";
            var prompt1Res = await client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/prompt", new { prompt = prompt1 });
            Assert.True(prompt1Res.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK);

            // Wait for Turn 1 to complete (watchdog timeout: 60s for live LLM)
            var state1 = await WaitForMessagesWithWatchdogAsync(client, provider, conversation.Id, minimumMessageCount: 2, timeout: TimeSpan.FromSeconds(60));
            if (state1 == null || state1.Messages.Count < 2)
            {
                Skip.If(true, $"Provider '{providerId}' did not produce a response within the 60s timeout window (CLI execution timed out or hung).");
                return;
            }

            var turn1AssistantMsg = state1.Messages.FirstOrDefault(m => m.Role == MessageRole.Assistant);
            Assert.NotNull(turn1AssistantMsg);

            // Skip if the live provider returned an API connection or credential error during live execution
            if (turn1AssistantMsg.Content.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
                turn1AssistantMsg.Content.Contains("ConnectionRefused", StringComparison.OrdinalIgnoreCase) ||
                turn1AssistantMsg.Content.Contains("API Error", StringComparison.OrdinalIgnoreCase) ||
                turn1AssistantMsg.Metadata?.IsSuccess == false)
            {
                Skip.If(true, $"Provider '{providerId}' returned an API execution error: {turn1AssistantMsg.Content}");
                return;
            }

            var session1 = turn1AssistantMsg.Metadata?.ProviderSessionId;

            // 5. Turn 2 (Recall Prompt)
            var prompt2 = "What was the number I asked you to remember in this session? Reply with only the number.";
            var prompt2Res = await client.PostAsJsonAsync($"/api/v1/conversations/{conversation.Id}/prompt", new { prompt = prompt2 });
            Assert.True(prompt2Res.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK);

            // Wait for Turn 2 to complete (minimum 4 messages)
            var state2 = await WaitForMessagesWithWatchdogAsync(client, provider, conversation.Id, minimumMessageCount: 4, timeout: TimeSpan.FromSeconds(60));
            if (state2 == null || state2.Messages.Count < 4)
            {
                Skip.If(true, $"Provider '{providerId}' did not complete Turn 2 within the 60s timeout window.");
                return;
            }

            var turn2AssistantMsg = state2.Messages.LastOrDefault(m => m.Role == MessageRole.Assistant);
            Assert.NotNull(turn2AssistantMsg);

            // 6. Assert Session Continuity
            if (!string.IsNullOrEmpty(session1))
            {
                Assert.Equal(session1, turn2AssistantMsg.Metadata?.ProviderSessionId);
            }

            // 7. Assert Memory Retention (LLM recalled the exact number)
            Assert.Contains(secret.ToString(), turn2AssistantMsg.Content);
        }
        finally
        {
            if (conversationId != Guid.Empty)
            {
                try { await provider.AbortAsync(conversationId); } catch { }
            }

            if (Directory.Exists(tempFolder))
            {
                try { Directory.Delete(tempFolder, true); } catch { }
            }
        }
    }

    private async Task<IProvider> CheckProviderAvailabilityOrSkipAsync(string providerId)
    {
        using var scope = _factory.Services.CreateScope();
        var providerManager = scope.ServiceProvider.GetRequiredService<IProviderManager>();
        var provider = providerManager.GetProvider(providerId);

        Skip.If(provider == null || !provider.IsInstalledFastCheck(), $"Provider '{providerId}' is not installed on this system.");

        var detection = await provider.DetectDetailedAsync();
        Skip.If(detection.Status != ProviderStatus.Ready, $"Provider '{providerId}' is not ready (Status: {detection.Status}, Message: {detection.Message}).");

        return provider;
    }

    private static async Task<ConversationDetailDto?> WaitForMessagesWithWatchdogAsync(
        HttpClient client,
        IProvider provider,
        Guid conversationId,
        int minimumMessageCount,
        TimeSpan timeout)
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
            await Task.Delay(500);
        }

        // Timeout exceeded - trigger watchdog abort on running CLI process
        try
        {
            await provider.AbortAsync(conversationId);
        }
        catch { }

        await Task.Delay(1000);

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
