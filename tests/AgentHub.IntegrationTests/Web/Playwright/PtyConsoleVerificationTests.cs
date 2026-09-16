using System.IO;
using Microsoft.Playwright;
using Xunit;

namespace AgentHub.IntegrationTests.Web.Playwright;

public class PtyConsoleVerificationTests
{
    private const string TargetConvUrl = "https://localhost:5432/workspaces/01094ced-ced0-4120-ae1d-b307c73eb6cb/conversations/cc783b16-8663-4e60-805f-114d626bbd70";

    [Fact]
    public async Task VerifyPtyTerminalRunsAgyConsoleApp()
    {
        try
        {
            _ = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        }
        catch { }

        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            SlowMo = 100
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
        });

        var page = await context.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER {msg.Type}]: {msg.Text}");

        // 1. Navigate to target conversation URL
        _ = await page.GotoAsync(TargetConvUrl);
        await page.WaitForSelectorAsync("#root", new PageWaitForSelectorOptions { Timeout = 15000 });

        // 2. Login if redirected to sign-in page
        var loginUser = page.Locator("#loginUsername");
        if (await loginUser.IsVisibleAsync())
        {
            await page.FillAsync("#loginUsername", "admin");
            await page.FillAsync("#loginPassword", "123123");
            await page.ClickAsync("#loginSubmitBtn");
            await page.WaitForTimeoutAsync(1500);

            // Re-navigate to target conversation after login
            _ = await page.GotoAsync(TargetConvUrl);
            await page.WaitForTimeoutAsync(1500);
        }

        // Wait for studio or main app nav
        _ = await page.WaitForSelectorAsync("#chatPanel, .studio-compact-header, #mainNav", new PageWaitForSelectorOptions { Timeout = 15000 });
        await page.WaitForTimeoutAsync(1000);

        // 3. Switch to Console (PTY) view
        var consoleToggleBtn = page.Locator("button:has-text('Console (PTY)')");
        await consoleToggleBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await consoleToggleBtn.ClickAsync();

        // 4. Wait for terminal container to mount
        var terminalWrapper = page.Locator(".terminal-canvas-wrapper");
        await terminalWrapper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Wait for shell prompt to render
        await page.WaitForTimeoutAsync(2000);

        // Restart PTY session to ensure clean state
        var restartBtn = page.Locator("button:has-text('Restart')");
        if (await restartBtn.IsVisibleAsync())
        {
            await restartBtn.ClickAsync();
            await page.WaitForTimeoutAsync(2500);
        }

        // Focus xterm instance
        await page.EvaluateAsync("() => { window.__term?.focus(); }");
        var xtermScreen = page.Locator(".xterm-screen");
        if (await xtermScreen.IsVisibleAsync())
        {
            await xtermScreen.ClickAsync();
        }
        else
        {
            await terminalWrapper.ClickAsync();
        }
        await page.WaitForTimeoutAsync(300);

        // 5. Test launching "cmd"
        await page.Keyboard.TypeAsync("cmd");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(2000);

        // 6. Test launching "agy" console app inside CMD
        await page.Keyboard.TypeAsync("agy");
        await page.Keyboard.PressAsync("Enter");

        page.Console += (_, e) => Console.Error.WriteLine($"[BROWSER {e.Type}] {e.Text}");
        page.PageError += (_, e) => Console.Error.WriteLine($"[PAGE ERROR] {e}");

        // Wait and poll for agy to initialize and render
        var capturedText = string.Empty;
        var foundExpected = false;
        for (var i = 0; i < 25; i++)
        {
            await page.WaitForTimeoutAsync(1500);
            capturedText = await page.EvaluateAsync<string>(@"() => {
                const term = window.__term;
                if (!term) return document.querySelector('.terminal-canvas-wrapper')?.innerText || '';
                const getBufText = (buf) => {
                    if (!buf) return '';
                    const lines = [];
                    for (let r = 0; r < buf.length; r++) {
                        const line = buf.getLine(r);
                        if (line) {
                            const s = line.translateToString(true);
                            if (s.trim().length > 0) lines.push(s);
                        }
                    }
                    return lines.join('\n');
                };
                const activeText = getBufText(term.buffer.active);
                const normalText = getBufText(term.buffer.normal);
                const altText = getBufText(term.buffer.alternate);
                const chunksReceived = window.__chunksReceived || 0;
                const lastChunk = window.__lastChunk || '';
                const allOutput = window.__allOutput || '';
                return `[CHUNKS: ${chunksReceived}]\n[ACTIVE (${term.buffer.active?.type})]:\n${activeText}\n[NORMAL]:\n${normalText}\n[ALTERNATE]:\n${altText}\n[LAST CHUNK]:\n${lastChunk.slice(0, 100)}\n[ALL OUTPUT]:\n${allOutput.slice(-500)}`;
            }");
            Console.Error.WriteLine($"[PTY T+{i * 1.5}s]:\n{capturedText}");

            if (capturedText.Contains("Antigravity") ||
                capturedText.Contains("Gemini") ||
                capturedText.Contains("shortcuts") ||
                capturedText.Contains("Google AI") ||
                capturedText.Contains("Signing in") ||
                capturedText.Contains("Welcome to the Antigravity") ||
                capturedText.Contains("Thought Process") ||
                capturedText.Contains("rafaelhbrasil@gmail.com") ||
                capturedText.Contains("Google AI Pro"))
            {
                foundExpected = true;
                break;
            }
        }

        // Take a screenshot of the terminal
        var screenshotDir = Path.Combine("C:\\tmp\\testing\\antigravity");
        _ = Directory.CreateDirectory(screenshotDir);
        var screenshotPath = Path.Combine(screenshotDir, "pty_agy_screenshot.png");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath });
        Console.WriteLine($"Screenshot saved to: {screenshotPath}");

        Assert.True(foundExpected, $"Terminal did not show expected Antigravity TUI output. Actual text:\n{capturedText}");
    }

    [Fact]
    public async Task VerifyPtyTerminalRunsDirCommand()
    {
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

        _ = await page.GotoAsync("https://127.0.0.1:5432");
        await page.WaitForSelectorAsync("#root", new PageWaitForSelectorOptions { Timeout = 15000 });

        var loginUser = page.Locator("#loginUsername");
        if (await loginUser.IsVisibleAsync())
        {
            await page.FillAsync("#loginUsername", "admin");
            await page.FillAsync("#loginPassword", "123123");
            await page.ClickAsync("#loginSubmitBtn");
            await page.WaitForTimeoutAsync(1000);
        }

        await page.ClickAsync("[data-tab=\"workspaces\"]");
        await page.WaitForTimeoutAsync(800);

        var openStudioBtn = page.Locator("button:has-text('Open Studio')").First;
        await openStudioBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await openStudioBtn.ClickAsync();
        await page.WaitForTimeoutAsync(1000);

        var consoleToggleBtn = page.Locator("button:has-text('Console (PTY)')");
        await consoleToggleBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await consoleToggleBtn.ClickAsync();

        var terminalWrapper = page.Locator(".terminal-canvas-wrapper");
        await terminalWrapper.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        await page.WaitForTimeoutAsync(2500);
        await page.EvaluateAsync("() => { window.__term?.focus(); }");
        var xtermScreen = page.Locator(".xterm-screen");
        if (await xtermScreen.IsVisibleAsync())
        {
            await xtermScreen.ClickAsync();
        }
        else
        {
            await terminalWrapper.ClickAsync();
        }
        await page.WaitForTimeoutAsync(300);

        await page.Keyboard.TypeAsync("dir");
        await page.Keyboard.PressAsync("Enter");

        var foundExpectedOutput = false;
        var capturedText = string.Empty;

        for (var attempt = 0; attempt < 20; attempt++)
        {
            await page.WaitForTimeoutAsync(500);
            capturedText = await page.EvaluateAsync<string>(@"() => {
                const term = window.__term;
                if (!term) return document.querySelector('.terminal-canvas-wrapper')?.innerText || '';
                const lines = [];
                const buf = term.buffer.active;
                for (let r = 0; r < buf.length; r++) {
                    const line = buf.getLine(r);
                    if (line) lines.push(line.translateToString(true));
                }
                return lines.join('\n');
            }");

            if (capturedText.Contains("first_directory") || capturedText.Contains("lorem_ipsum") || capturedText.Contains("README.md"))
            {
                foundExpectedOutput = true;
                break;
            }
        }

        Assert.True(foundExpectedOutput, $"Terminal did not output expected directory contents. Actual output: {capturedText}");
    }
}
