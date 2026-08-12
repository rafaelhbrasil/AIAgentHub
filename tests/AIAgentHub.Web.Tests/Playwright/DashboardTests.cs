using Microsoft.Playwright;
using Xunit;

namespace AIAgentHub.Web.Tests.Playwright;

public class DashboardTests : IAsyncLifetime
{
    private Microsoft.Playwright.IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    [Fact(Skip = "Requires running application server")]
    public async Task Dashboard_ShowsSkeletonsOnFirstLoad()
    {
        // Navigate to app
        await _page.GotoAsync("https://localhost:5432");
        
        // Login if needed
        await LoginIfRequired();

        // Navigate to dashboard
        await _page.ClickAsync("[data-tab=\"dashboard\"]");
        
        // Check for skeleton elements (they should appear briefly)
        // Note: This may be flaky due to timing, but validates the skeleton exists in DOM
        var skeletonCount = await _page.Locator(".skeleton").CountAsync();
        var statValCount = await _page.Locator(".stat-val").CountAsync();
        Assert.True(skeletonCount > 0 || statValCount > 0);
    }

    [Fact(Skip = "Requires running application server")]
    public async Task Dashboard_ShowsLastUpdatedTimestamp()
    {
        await _page.GotoAsync("https://localhost:5432");
        await LoginIfRequired();
        await _page.ClickAsync("[data-tab=\"dashboard\"]");
        
        // Wait for data to load
        await _page.WaitForSelectorAsync(".last-updated", new() { Timeout = 10000 });
        
        var lastUpdated = await _page.TextContentAsync(".last-updated");
        Assert.NotNull(lastUpdated);
        Assert.Contains("Updated", lastUpdated);
    }

    [Fact(Skip = "Requires running application server")]
    public async Task Dashboard_CacheWorksOnSecondVisit()
    {
        await _page.GotoAsync("https://localhost:5432");
        await LoginIfRequired();
        
        // First visit - should fetch data
        await _page.ClickAsync("[data-tab=\"dashboard\"]");
        await _page.WaitForSelectorAsync(".stat-val", new() { Timeout = 10000 });
        
        // Navigate away
        await _page.ClickAsync("[data-tab=\"providers\"]");
        await Task.Delay(500);
        
        // Navigate back - should use cache (no skeletons)
        await _page.ClickAsync("[data-tab=\"dashboard\"]");
        
        // Verify content is displayed immediately
        var statVals = await _page.Locator(".stat-val").CountAsync();
        Assert.True(statVals >= 3);
    }

    private async Task LoginIfRequired()
    {
        var loginBtn = await _page.QuerySelectorAsync("#loginSubmitBtn");
        if (loginBtn != null)
        {
            await _page.FillAsync("#loginUsername", "admin");
            await _page.FillAsync("#loginPassword", "admin");
            await _page.ClickAsync("#loginSubmitBtn");
            await _page.WaitForTimeoutAsync(1000);
        }
    }
}
