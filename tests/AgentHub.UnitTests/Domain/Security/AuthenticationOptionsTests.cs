using AIAgentHub.Domain.Configuration;
using AIAgentHub.Web;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgentHub.UnitTests.Domain.Security;

public sealed class AuthenticationOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeAuthentication()
    {
        // Assert
        Assert.Equal("Authentication", AuthenticationOptions.SectionName);
    }

    [Fact]
    public void Properties_ShouldSetAndGetValues()
    {
        // Arrange
        var options = new AuthenticationOptions
        {
            CookieName = "Custom.Cookie.Name"
        };

        // Assert
        Assert.Equal("Custom.Cookie.Name", options.CookieName);
    }

    [Fact]
    public void AddAgentHubServices_WhenCookieNameMissing_ShouldUseEnvironmentDefault()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var services = new ServiceCollection();

        // Act
        services.AddAgentHubServices(configuration);
        using var sp = services.BuildServiceProvider();

        // Assert
        var cookieOptions = sp.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        Assert.False(string.IsNullOrWhiteSpace(cookieOptions.Cookie.Name));
        Assert.StartsWith("AIAgentHub.Session", cookieOptions.Cookie.Name);
    }

    [Fact]
    public void AddAgentHubServices_WhenCookieNameProvided_ShouldConfigureCookieAndOptions()
    {
        // Arrange
        var expectedCookieName = "AIAgentHub.Session.Dev";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:CookieName"] = expectedCookieName
            })
            .Build();
        var services = new ServiceCollection();

        // Act
        services.AddAgentHubServices(configuration);
        using var sp = services.BuildServiceProvider();

        // Assert AuthenticationOptions registered
        var authOptions = sp.GetRequiredService<AuthenticationOptions>();
        Assert.Equal(expectedCookieName, authOptions.CookieName);

        // Assert CookieAuthenticationOptions configured
        var cookieOptions = sp.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        Assert.Equal(expectedCookieName, cookieOptions.Cookie.Name);
        Assert.True(cookieOptions.Cookie.HttpOnly);
    }
}
