using AIAgentHub.Web.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace AgentHub.UnitTests.Controllers;

public class SystemControllerTests
{
    [Theory]
    [InlineData("0.1.1.0", "0.1.1")]
    [InlineData("1.0.0.0", "1.0.0")]
    [InlineData("0.1.1.830", "0.1.1.830")]
    [InlineData("0.1.1.0830", "0.1.1.0830")]
    [InlineData("0.1.1", "0.1.1")]
    [InlineData("invalid-version", "invalid-version")]
    public void FormatDisplayVersion_OmitsZeroRevision_PreservesDebugBuild(string rawVersion, string expected)
    {
        var result = SystemController.FormatDisplayVersion(rawVersion);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetVersion_ReturnsExpectedStructure()
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns("Production");

        var controller = new SystemController(environment);
        var actionResult = controller.GetVersion();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<SystemController.SystemVersionResponse>(okResult.Value);

        Assert.NotNull(response.Version);
        Assert.False(string.IsNullOrEmpty(response.Version));
        var parts = response.Version.Split('.');
        if (parts.Length == 4)
        {
            Assert.NotEqual("0", parts[3]);
        }
        Assert.Equal("Production", response.Environment);
    }
}
