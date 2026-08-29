using System.Net;
using AIAgentHub.Application.Security;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Security;
using AIAgentHub.Web.Controllers;
using AIAgentHub.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace AgentHub.UnitTests.Web;

public class SafeClientAndRecoveryTests
{
    [Theory]
    [InlineData("127.0.0.1", null, true)]
    [InlineData("::1", null, true)]
    [InlineData("192.168.1.50", "192.168.1.50", true)]
    [InlineData("::ffff:192.168.1.50", "192.168.1.50", true)]
    [InlineData("192.168.1.50", "::ffff:192.168.1.50", true)]
    [InlineData("192.168.1.99", "192.168.1.50", false)]
    [InlineData("10.0.0.1", null, false)]
    [InlineData("10.0.0.1", "", false)]
    [InlineData("10.0.0.1", "invalid-ip", false)]
    public void RecoveryOptions_IsSafeClientOrLoopback_EvaluatesCorrectly(string remoteIpStr, string? safeClientIp, bool expected)
    {
        var options = new RecoveryOptions
        {
            IsRecoveryModeEnabled = true,
            SafeClientIp = safeClientIp
        };

        var remoteIp = IPAddress.Parse(remoteIpStr);
        var result = options.IsSafeClientOrLoopback(remoteIp);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task NetworkModeMiddleware_LocalhostMode_AllowsSafeClientIp()
    {
        var recoveryOptions = new RecoveryOptions
        {
            IsRecoveryModeEnabled = false,
            SafeClientIp = "192.168.1.50"
        };

        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new NetworkModeMiddleware(next, recoveryOptions);

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.50");

        var settingsRepo = Substitute.For<IServerSettingsRepository>();
        settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns(new ServerSettings
        {
            NetworkMode = NetworkMode.Localhost
        });

        await middleware.InvokeAsync(context, settingsRepo);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task NetworkModeMiddleware_LocalhostMode_BlocksNonSafeClientRemoteIp()
    {
        var recoveryOptions = new RecoveryOptions
        {
            IsRecoveryModeEnabled = false,
            SafeClientIp = "192.168.1.50"
        };

        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new NetworkModeMiddleware(next, recoveryOptions);

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.99");

        var settingsRepo = Substitute.For<IServerSettingsRepository>();
        settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns(new ServerSettings
        {
            NetworkMode = NetworkMode.Localhost
        });

        await middleware.InvokeAsync(context, settingsRepo);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task AuthController_GetSetupStatus_WithSafeClientAndRecovery_EnablesResetWithoutCode()
    {
        var setupService = Substitute.For<ISetupService>();
        setupService.IsSetupCompletedAsync(Arg.Any<CancellationToken>()).Returns(true);

        var authService = Substitute.For<IAuthenticationService>();
        var recoveryOptions = new RecoveryOptions
        {
            IsRecoveryModeEnabled = true,
            SafeClientIp = "192.168.1.50"
        };

        var controller = new AuthController(setupService, authService, recoveryOptions)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.50");

        var actionResult = await controller.GetSetupStatus(CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);

        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("isSetupCompleted").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("isRecoveryModeEnabled").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("isLocalRequest").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("canResetWithoutCode").GetBoolean());
    }

    [Fact]
    public async Task AuthController_RecoverWipe_FromSafeClientWithRecovery_Succeeds()
    {
        var setupService = Substitute.For<ISetupService>();
        setupService.WipeAllDataAsync(Arg.Any<CancellationToken>()).Returns(true);

        var authService = Substitute.For<IAuthenticationService>();
        var recoveryOptions = new RecoveryOptions
        {
            IsRecoveryModeEnabled = true,
            SafeClientIp = "192.168.1.50"
        };

        var aspAuthService = Substitute.For<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(Microsoft.AspNetCore.Authentication.IAuthenticationService)).Returns(aspAuthService);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.50");

        var controller = new AuthController(setupService, authService, recoveryOptions)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        var actionResult = await controller.RecoverWipe(CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);

        await setupService.Received(1).WipeAllDataAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthController_RecoverWipe_FromUnauthorizedRemoteIp_ReturnsForbid()
    {
        var setupService = Substitute.For<ISetupService>();
        var authService = Substitute.For<IAuthenticationService>();
        var recoveryOptions = new RecoveryOptions
        {
            IsRecoveryModeEnabled = true,
            SafeClientIp = "192.168.1.50"
        };

        var controller = new AuthController(setupService, authService, recoveryOptions)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.99");

        var actionResult = await controller.RecoverWipe(CancellationToken.None);
        Assert.IsType<ForbidResult>(actionResult);

        await setupService.DidNotReceive().WipeAllDataAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthController_ResetSetup_FromSafeClientWithRecovery_Succeeds()
    {
        var setupService = Substitute.For<ISetupService>();
        setupService.IsSetupCompletedAsync(Arg.Any<CancellationToken>()).Returns(true);
        setupService.ResetToSetupModeAsync(null, Arg.Any<CancellationToken>()).Returns(true);

        var authService = Substitute.For<IAuthenticationService>();
        var recoveryOptions = new RecoveryOptions
        {
            IsRecoveryModeEnabled = true,
            SafeClientIp = "192.168.1.50"
        };

        var aspAuthService = Substitute.For<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(Microsoft.AspNetCore.Authentication.IAuthenticationService)).Returns(aspAuthService);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.50");

        var controller = new AuthController(setupService, authService, recoveryOptions)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        var actionResult = await controller.ResetSetup(CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);

        await setupService.Received(1).ResetToSetupModeAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthController_ResetSetup_FromUnauthorizedRemoteIp_ReturnsForbid()
    {
        var setupService = Substitute.For<ISetupService>();
        setupService.IsSetupCompletedAsync(Arg.Any<CancellationToken>()).Returns(true);

        var authService = Substitute.For<IAuthenticationService>();
        var recoveryOptions = new RecoveryOptions
        {
            IsRecoveryModeEnabled = true,
            SafeClientIp = "192.168.1.50"
        };

        var controller = new AuthController(setupService, authService, recoveryOptions)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.99");

        var actionResult = await controller.ResetSetup(CancellationToken.None);
        Assert.IsType<ForbidResult>(actionResult);

        await setupService.DidNotReceive().ResetToSetupModeAsync(null, Arg.Any<CancellationToken>());
    }
}
