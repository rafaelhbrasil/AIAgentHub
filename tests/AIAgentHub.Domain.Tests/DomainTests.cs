using AIAgentHub.Domain.Common;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Mcp;
using AIAgentHub.Domain.Permissions;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Domain.Security;
using AIAgentHub.Domain.Skills;
using AIAgentHub.Domain.Workspaces;

namespace AIAgentHub.Domain.Tests;

public sealed class DomainTests
{
    private class TestEntity : Entity { }
    private class TestAggregateRoot : AggregateRoot { }

    [Fact]
    public void Entity_And_AggregateRoot_ShouldInitializeWithGuid()
    {
        var entity = new TestEntity();
        var root = new TestAggregateRoot();

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.NotEqual(Guid.Empty, root.Id);
    }

    [Fact]
    public void Workspace_Create_ShouldInitializeCorrectly()
    {
        var ws = Workspace.Create("AgentHub", "D:\\Code\\AgentHub");
        Assert.NotEqual(Guid.Empty, ws.Id);
        Assert.Equal("AgentHub", ws.Name);
        Assert.Contains("AgentHub", ws.Path);
        Assert.Equal(WorkspaceOrigin.Server, ws.Origin);
        Assert.NotNull(ws.Settings);
        Assert.Empty(ws.Conversations);
    }

    [Fact]
    public void Workspace_Create_InvalidNameOrPath_ShouldThrow()
    {
        _ = Assert.Throws<ArgumentException>(() => Workspace.Create("", "D:\\Path"));
        _ = Assert.Throws<ArgumentException>(() => Workspace.Create("   ", "D:\\Path"));
        _ = Assert.Throws<ArgumentException>(() => Workspace.Create("Name", ""));
        _ = Assert.Throws<ArgumentException>(() => Workspace.Create("Name", "   "));
    }

    [Fact]
    public void Workspace_Rename_ShouldUpdateNameAndTouch()
    {
        var ws = Workspace.Create("OldName", "D:\\Code\\Test");
        var originalAccess = ws.LastAccessedAtUtc;

        ws.Rename("NewName");
        Assert.Equal("NewName", ws.Name);
        Assert.True(ws.LastAccessedAtUtc >= originalAccess);
    }

    [Fact]
    public void Workspace_Rename_EmptyName_ShouldThrow()
    {
        var ws = Workspace.Create("Name", "D:\\Code\\Test");
        _ = Assert.Throws<ArgumentException>(() => ws.Rename(""));
        _ = Assert.Throws<ArgumentException>(() => ws.Rename("   "));
    }

    [Fact]
    public void Workspace_UpdateSettings_ShouldSetSettingsAndTouch()
    {
        var ws = Workspace.Create("Name", "D:\\Code\\Test");
        var settings = new WorkspaceSettings { DefaultProviderId = "opencode", DefaultModelId = "gpt-5" };

        ws.UpdateSettings(settings);
        Assert.Equal("opencode", ws.Settings.DefaultProviderId);
        Assert.Equal("gpt-5", ws.Settings.DefaultModelId);

        _ = Assert.Throws<ArgumentNullException>(() => ws.UpdateSettings(null!));
    }

    [Fact]
    public void Conversation_Create_And_AddMessage_ShouldWork()
    {
        var wsId = Guid.NewGuid();
        var conv = Conversation.Create(wsId, "Initial Task", "gemini", "gemini-2.5-pro", "session-1", "high");

        Assert.Equal("Initial Task", conv.Title);
        Assert.Equal("gemini", conv.ProviderId);
        Assert.Equal("gemini-2.5-pro", conv.ModelId);
        Assert.Equal("session-1", conv.ProviderSessionId);
        Assert.Equal("high", conv.Effort);

        var msg = conv.AddMessage(MessageRole.User, "Hello AI");
        _ = Assert.Single(conv.Messages);
        Assert.Equal(MessageRole.User, msg.Role);
        Assert.Equal("Hello AI", msg.Content);
    }

    [Fact]
    public void Conversation_Create_InvalidWorkspace_ShouldThrow() => _ = Assert.Throws<ArgumentException>(() => Conversation.Create(Guid.Empty, "Title"));

    [Fact]
    public void Conversation_Create_DefaultTitleAndProvider_Fallback()
    {
        var conv = Conversation.Create(Guid.NewGuid(), "", "  ");
        Assert.Equal("New Conversation", conv.Title);
        Assert.Equal("gemini", conv.ProviderId);
    }

    [Fact]
    public void Conversation_Rename_SetProviderModel_SetEffort_SetSessionId()
    {
        var conv = Conversation.Create(Guid.NewGuid(), "Title");

        conv.Rename("Updated Title");
        Assert.Equal("Updated Title", conv.Title);
        _ = Assert.Throws<ArgumentException>(() => conv.Rename(""));

        conv.SetProviderAndModel("claude", "claude-3-7-sonnet", "medium");
        Assert.Equal("claude", conv.ProviderId);
        Assert.Equal("claude-3-7-sonnet", conv.ModelId);
        Assert.Equal("medium", conv.Effort);
        _ = Assert.Throws<ArgumentException>(() => conv.SetProviderAndModel("", "model"));

        conv.SetEffort("low");
        Assert.Equal("low", conv.Effort);

        conv.SetProviderSessionId("ses-123");
        Assert.Equal("ses-123", conv.ProviderSessionId);
    }

    [Fact]
    public void Conversation_AddFileChange_ShouldAppendAndTouch()
    {
        var conv = Conversation.Create(Guid.NewGuid(), "Title");
        var change = FileChange.Create(conv.Id, "src/index.js", FileChangeType.Created);

        conv.AddFileChange(change);
        _ = Assert.Single(conv.FileChanges);
        _ = Assert.Throws<ArgumentNullException>(() => conv.AddFileChange(null!));
    }

    [Fact]
    public void Message_Create_Validations()
    {
        var convId = Guid.NewGuid();
        var meta = new ExecutionMetadata { Action = "test", DurationMs = 100, Tokens = 50, IsSuccess = true };
        var msg = Message.Create(convId, MessageRole.Assistant, "AI Output", meta);

        Assert.NotEqual(Guid.Empty, msg.Id);
        Assert.Equal(convId, msg.ConversationId);
        Assert.Equal(MessageRole.Assistant, msg.Role);
        Assert.Equal("AI Output", msg.Content);
        Assert.Equal(meta, msg.Metadata);

        _ = Assert.Throws<ArgumentException>(() => Message.Create(Guid.Empty, MessageRole.User, "test"));

        var emptyMsg = Message.Create(convId, MessageRole.User, null!);
        Assert.Equal(string.Empty, emptyMsg.Content);
    }

    [Fact]
    public void ExecutionMetadata_PropertySetters()
    {
        var meta = new ExecutionMetadata
        {
            ProviderId = "opencode",
            ModelId = "gpt-5",
            ProviderSessionId = "ses-1",
            Action = "prompt",
            DurationMs = 250,
            Tokens = 120,
            IsSuccess = false,
            ErrorMessage = "Failed execution"
        };

        Assert.Equal("opencode", meta.ProviderId);
        Assert.Equal("gpt-5", meta.ModelId);
        Assert.Equal("ses-1", meta.ProviderSessionId);
        Assert.Equal("prompt", meta.Action);
        Assert.Equal(250, meta.DurationMs);
        Assert.Equal(120, meta.Tokens);
        Assert.False(meta.IsSuccess);
        Assert.Equal("Failed execution", meta.ErrorMessage);
    }

    [Fact]
    public void FileChange_Create_Accept_Reject()
    {
        var convId = Guid.NewGuid();
        var change = FileChange.Create(convId, "\\src\\Program.cs", FileChangeType.Modified, "snapshot/path");

        Assert.Equal("src/Program.cs", change.RelativePath);
        Assert.Equal(FileChangeType.Modified, change.ChangeType);
        Assert.Equal("snapshot/path", change.SnapshotPath);
        Assert.Equal(ReviewStatus.Pending, change.Status);
        Assert.Null(change.ReviewedAtUtc);

        change.Accept();
        Assert.Equal(ReviewStatus.Accepted, change.Status);
        _ = Assert.NotNull(change.ReviewedAtUtc);

        change.Reject();
        Assert.Equal(ReviewStatus.Rejected, change.Status);

        _ = Assert.Throws<ArgumentException>(() => FileChange.Create(Guid.Empty, "src/path", FileChangeType.Created));
        _ = Assert.Throws<ArgumentException>(() => FileChange.Create(convId, "", FileChangeType.Created));
    }

    [Fact]
    public void FileSnapshot_Create_PathNormalization()
    {
        var wsId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var snap = FileSnapshot.Create(wsId, convId, "\\src\\App.cs", "key-123", "hash-abc", 1024);

        Assert.Equal(wsId, snap.WorkspaceId);
        Assert.Equal(convId, snap.ConversationId);
        Assert.Equal("src/App.cs", snap.RelativePath);
        Assert.Equal("key-123", snap.StorageKey);
        Assert.Equal("hash-abc", snap.FileHash);
        Assert.Equal(1024, snap.Size);
    }

    [Fact]
    public void UserAccount_And_EncryptedSecret()
    {
        var user = UserAccount.Create("admin", "hash123", "salt123", "recHash123");
        Assert.Equal("admin", user.Username);
        Assert.Equal("hash123", user.PasswordHash);
        Assert.Equal("salt123", user.PasswordSalt);
        Assert.Equal("recHash123", user.RecoveryCodeHash);
        Assert.Null(user.LastLoginAtUtc);

        user.RecordLogin();
        _ = Assert.NotNull(user.LastLoginAtUtc);

        user.UpdatePassword("newHash", "newSalt");
        Assert.Equal("newHash", user.PasswordHash);
        Assert.Equal("newSalt", user.PasswordSalt);

        _ = Assert.Throws<ArgumentException>(() => UserAccount.Create("", "h", "s", "r"));

        var secret = new EncryptedSecret
        {
            ProviderId = "opencode",
            KeyName = "API_KEY",
            CiphertextBase64 = "cipher",
            NonceBase64 = "nonce",
            TagBase64 = "tag"
        };
        Assert.Equal("opencode", secret.ProviderId);
        Assert.Equal("API_KEY", secret.KeyName);
        Assert.Equal("cipher", secret.CiphertextBase64);
    }

    [Fact]
    public void ServerSettings_Properties()
    {
        var server = new ServerSettings
        {
            IsSetupCompleted = true,
            NetworkMode = NetworkMode.Lan,
            ListeningPortHttps = 8443,
            ListeningPortHttp = 8080,
            SelectedInterfaces = ["127.0.0.1"],
            Theme = "light"
        };

        Assert.True(server.IsSetupCompleted);
        Assert.Equal(NetworkMode.Lan, server.NetworkMode);
        Assert.Equal(8443, server.ListeningPortHttps);
        Assert.Equal(8080, server.ListeningPortHttp);
        _ = Assert.Single(server.SelectedInterfaces);
        Assert.Equal("light", server.Theme);
    }

    [Fact]
    public void PermissionRequest_Create_And_Decide()
    {
        var convId = Guid.NewGuid();
        var req = PermissionRequest.Create(convId, "gemini", PermissionType.FileWrite, "src/main.cs", "modifying code");

        Assert.Equal(convId, req.ConversationId);
        Assert.Equal("gemini", req.ProviderId);
        Assert.Equal(PermissionType.FileWrite, req.Type);
        Assert.Equal("src/main.cs", req.Target);
        Assert.Equal("modifying code", req.Reason);
        Assert.Equal(PermissionDecision.Pending, req.Decision);
        Assert.Null(req.DecidedAtUtc);

        req.Decide(true);
        Assert.Equal(PermissionDecision.Approved, req.Decision);
        _ = Assert.NotNull(req.DecidedAtUtc);

        req.Decide(false);
        Assert.Equal(PermissionDecision.Denied, req.Decision);

        var fallbackReq = PermissionRequest.Create(convId, null!, PermissionType.DirectoryAccess, null!, null!);
        Assert.Equal(string.Empty, fallbackReq.ProviderId);
        Assert.Equal(string.Empty, fallbackReq.Target);
        Assert.Equal(string.Empty, fallbackReq.Reason);
    }

    [Fact]
    public void McpServer_Skill_ProviderModels_CliOptions_Properties()
    {
        var mcp = new McpServer
        {
            Name = "Server1",
            Command = "npx",
            Arguments = "-y test",
            EnvironmentVariables = new() { { "ENV", "VAL" } },
            IsEnabled = true,
            Tools = [new() { Name = "tool1", Description = "desc", InputSchemaJson = "{}" }]
        };
        Assert.Equal("Server1", mcp.Name);
        _ = Assert.Single(mcp.Tools);

        var skill = new Skill
        {
            Name = "skill1",
            Description = "desc",
            Author = "author",
            ProviderId = "opencode",
            IsEnabled = true,
            FilePath = "/path",
            Content = "content"
        };
        Assert.Equal("skill1", skill.Name);

        var info = new ProviderInfo
        {
            Id = "p1",
            DisplayName = "P1",
            Description = "D1",
            IsInstalled = true,
            IsAuthenticated = true,
            Status = ProviderStatus.Ready,
            Message = "Msg",
            Version = "1.0",
            ExecutablePath = "/bin",
            Capabilities = ProviderCapability.Streaming,
            SupportedModels = [new() { Id = "m1", DisplayName = "M1", Description = "Desc", ContextWindow = 100, IsDefault = true, IsDisplayed = true }],
            InstallInstructions = "inst",
            InstallCommand = "cmd",
            AuthCommand = "auth",
            DocumentationUrl = "doc"
        };
        Assert.Equal("p1", info.Id);
        _ = Assert.Single(info.SupportedModels);

        var setting = new ProviderModelSetting { ProviderId = "p1", ModelId = "m1", IsDisplayed = false };
        Assert.Equal("p1", setting.ProviderId);
        Assert.False(setting.IsDisplayed);

        var record = new ProviderDetectionRecord
        {
            ProviderId = "p1",
            Status = ProviderStatus.Ready,
            StatusDetails = "details",
            Version = "1.0",
            ExecutablePath = "/bin",
            IsInstalled = true,
            IsAuthenticated = true,
            QuotaResetsAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        Assert.Equal("p1", record.ProviderId);
        _ = Assert.NotNull(record.QuotaResetsAt);

        var options = new CliExecutionOptions { Headless = false, Shell = "Bash", HeadedAutoCloseDelaySeconds = 15 };
        Assert.False(options.Headless);
        Assert.Equal("Bash", options.Shell);
        Assert.Equal(15, options.HeadedAutoCloseDelaySeconds);
    }
}
