using AIAgentHub.Domain.Conversations;
using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Security;
using AIAgentHub.Domain.Workspaces;
using Xunit;

namespace AIAgentHub.Domain.Tests;

public sealed class DomainTests
{
    [Fact]
    public void Workspace_Create_ShouldInitializeCorrectly()
    {
        var ws = Workspace.Create("AgentHub", "D:\\Code\\AgentHub");
        Assert.NotEqual(Guid.Empty, ws.Id);
        Assert.Equal("AgentHub", ws.Name);
        Assert.Contains("AgentHub", ws.Path);
        Assert.Equal(WorkspaceOrigin.Server, ws.Origin);
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
    public void Conversation_Create_And_AddMessage_ShouldWork()
    {
        var wsId = Guid.NewGuid();
        var conv = Conversation.Create(wsId, "Initial Task", "gemini", "gemini-2.5-pro");

        Assert.Equal("Initial Task", conv.Title);
        Assert.Equal("gemini", conv.ProviderId);
        Assert.Equal("gemini-2.5-pro", conv.ModelId);

        var msg = conv.AddMessage(MessageRole.User, "Hello AI");
        Assert.Single(conv.Messages);
        Assert.Equal(MessageRole.User, msg.Role);
        Assert.Equal("Hello AI", msg.Content);
    }

    [Fact]
    public void FileChange_AcceptAndReject_ShouldUpdateStatus()
    {
        var convId = Guid.NewGuid();
        var change = FileChange.Create(convId, "src/Program.cs", FileChangeType.Modified, "snapshot-key");

        Assert.Equal(ReviewStatus.Pending, change.Status);

        change.Accept();
        Assert.Equal(ReviewStatus.Accepted, change.Status);
        Assert.NotNull(change.ReviewedAtUtc);

        change.Reject();
        Assert.Equal(ReviewStatus.Rejected, change.Status);
    }

    [Fact]
    public void UserAccount_Create_ShouldInitializeProperly()
    {
        var user = UserAccount.Create("admin", "hash123", "salt123", "recHash123");
        Assert.Equal("admin", user.Username);
        Assert.Equal("hash123", user.PasswordHash);
        Assert.Null(user.LastLoginAtUtc);

        user.RecordLogin();
        Assert.NotNull(user.LastLoginAtUtc);
    }
}
