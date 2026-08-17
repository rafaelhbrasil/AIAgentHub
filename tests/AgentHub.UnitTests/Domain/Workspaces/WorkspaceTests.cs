using AIAgentHub.Domain.Workspaces;

namespace AgentHub.UnitTests.Domain.Workspaces;

public sealed class WorkspaceTests
{
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
}
