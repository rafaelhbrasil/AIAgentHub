using System.IO;
using Microsoft.Playwright;
using Xunit;

namespace AgentHub.IntegrationTests.Web.Playwright;

public class ThemeVerificationTests
{
    private const string TargetUrl = "https://127.0.0.1:5432";
    private readonly string _screenshotDir = Path.Combine(AppContext.BaseDirectory, "theme_screenshots");

    [Fact]
    public async Task CaptureAllPagesInLightTheme()
    {
        Directory.CreateDirectory(_screenshotDir);

        try
        {
            _ = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        }
        catch { }

        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
        });

        var page = await context.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER {msg.Type}]: {msg.Text}");

        // 1. Navigate to running server
        _ = await page.GotoAsync(TargetUrl);
        await page.WaitForSelectorAsync("#root", new PageWaitForSelectorOptions { Timeout = 15000 });

        // 2. Login if on sign in page
        var loginUser = page.Locator("#loginUsername");
        if (await loginUser.IsVisibleAsync())
        {
            // First capture dark sign-in page
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(_screenshotDir, "00-signin-dark.png") });

            // Switch to light theme on sign-in page if toggle exists
            var initialThemeBtn = page.Locator(".theme-toggle-btn");
            if (await initialThemeBtn.IsVisibleAsync())
            {
                await initialThemeBtn.ClickAsync();
                await page.WaitForTimeoutAsync(300);
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(_screenshotDir, "00b-signin-light.png") });
            }

            await page.FillAsync("#loginUsername", "admin");
            await page.FillAsync("#loginPassword", "123123");
            await page.ClickAsync("#loginSubmitBtn");
            await page.WaitForTimeoutAsync(1000);
        }

        // Wait for main app nav
        _ = await page.WaitForSelectorAsync("#mainNav, .theme-toggle-btn", new PageWaitForSelectorOptions { Timeout = 15000 });

        // 3. Switch Theme to Light
        var themeBtn = page.Locator(".theme-toggle-btn");
        var htmlClass = await page.EvaluateAsync<string>("() => document.documentElement.className");
        if (!htmlClass.Contains("light"))
        {
            await themeBtn.ClickAsync();
            htmlClass = await page.EvaluateAsync<string>("() => document.documentElement.className");
            if (!htmlClass.Contains("light"))
            {
                await themeBtn.ClickAsync(); // In case theme was on system
            }
        }

        // 4. Capture Dashboard in Light Theme
        await page.ClickAsync("[data-tab=\"dashboard\"]");
        await page.WaitForTimeoutAsync(800);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(_screenshotDir, "01-dashboard-light.png"), FullPage = true });

        // 5. Capture Workspaces in Light Theme
        await page.ClickAsync("[data-tab=\"workspaces\"]");
        await page.WaitForTimeoutAsync(800);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(_screenshotDir, "02-workspaces-light.png"), FullPage = true });

        // Open Folder Explorer Modal
        var addWsBtn = page.Locator("#dashNewWsBtn, button:has-text('+ Add Workspace'), button:has-text('+ Open or Create Workspace')");
        if (await addWsBtn.First.IsVisibleAsync())
        {
            await addWsBtn.First.ClickAsync();
            await page.WaitForTimeoutAsync(600);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(_screenshotDir, "02b-folder-explorer-modal-light.png") });

            var cancelModalBtn = page.Locator(".modal-close, .modal-footer button:has-text('Cancel')");
            if (await cancelModalBtn.First.IsVisibleAsync())
            {
                await cancelModalBtn.First.ClickAsync();
                await page.WaitForTimeoutAsync(400);
            }
        }

        // Open Studio on first workspace
        var openStudioBtn = page.Locator("button:has-text('Open Studio')");
        if (await openStudioBtn.First.IsVisibleAsync())
        {
            await openStudioBtn.First.ClickAsync();
            await page.WaitForTimeoutAsync(1000);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(_screenshotDir, "02c-studio-light.png"), FullPage = true });

            // Toggle Actions dropdown in Studio
            var actionsDropdownBtn = page.Locator(".studio-compact-header .icon-btn, #studioActionMenuBtn");
            if (await actionsDropdownBtn.Last.IsVisibleAsync())
            {
                await actionsDropdownBtn.Last.ClickAsync();
                await page.WaitForTimeoutAsync(400);
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(_screenshotDir, "02d-studio-actions-menu-light.png") });
                // Dismiss dropdown
                await page.Keyboard.PressAsync("Escape");
            }

            // Go back to workspaces
            var backBtn = page.Locator("#backToWsList");
            if (await backBtn.IsVisibleAsync())
            {
                await backBtn.ClickAsync();
                await page.WaitForTimeoutAsync(400);
            }
        }

        // 6. Capture Providers in Light Theme
        await page.ClickAsync("[data-tab=\"providers\"]");
        await page.WaitForTimeoutAsync(800);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(_screenshotDir, "03-providers-light.png"), FullPage = true });

        // Open Models Modal
        var modelsLink = page.Locator(".btn-link-inline");
        if (await modelsLink.First.IsVisibleAsync())
        {
            await modelsLink.First.ClickAsync();
            await page.WaitForTimeoutAsync(600);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(_screenshotDir, "03b-models-modal-light.png") });

            var cancelModalBtn = page.Locator(".modal-close, .modal-footer button:has-text('Cancel')");
            if (await cancelModalBtn.First.IsVisibleAsync())
            {
                await cancelModalBtn.First.ClickAsync();
                await page.WaitForTimeoutAsync(400);
            }
        }

        // 7. Capture MCPs & Skills in Light Theme
        await page.ClickAsync("[data-tab=\"tools\"]");
        await page.WaitForTimeoutAsync(800);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(_screenshotDir, "04-tools-light.png"), FullPage = true });

        // 8. Capture Settings in Light Theme
        await page.ClickAsync("[data-tab=\"settings\"]");
        await page.WaitForTimeoutAsync(800);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(_screenshotDir, "05-settings-light.png"), FullPage = true });

        // 9. Mobile Viewport Test (375x812)
        await page.SetViewportSizeAsync(375, 812);
        await page.WaitForTimeoutAsync(400);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(_screenshotDir, "06-settings-mobile-light.png") });

        // Open Mobile Burger Menu
        var burgerBtn = page.Locator(".burger-menu-btn");
        if (await burgerBtn.IsVisibleAsync())
        {
            await burgerBtn.ClickAsync();
            await page.WaitForTimeoutAsync(400);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(_screenshotDir, "07-mobile-drawer-light.png") });
        }
    }
}
