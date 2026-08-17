using AIAgentHub.Domain.Mcp;

namespace AgentHub.UnitTests.Domain.Mcp;

public sealed class McpServerTests
{
    [Fact]
    public void McpServer_Properties()
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
    }
}
