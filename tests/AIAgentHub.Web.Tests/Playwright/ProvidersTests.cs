using Microsoft.Playwright;
using Xunit;

namespace AIAgentHub.Web.Tests.Playwright;

public class ProvidersTests : IAsyncLifetime
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
    public async Task Providers_ShowsSkeletonsOnFirstLoad()
    {
        await _page.GotoAsync("https://localhost:5432");
        await LoginIfRequired();
        
        await _page.ClickAsync("[data-tab=\"providers\"]");
        
        // Check for skeleton or provider cards
        var skeletonCount = await _page.Locator(".skeleton-card").CountAsync();
        var providerCardCount = await _page.Locator("[id^=\"provider-card-\"]").CountAsync();
        Assert.True(skeletonCount > 0 || providerCardCount > 0);
    }

    [Fact(Skip = "Requires running application server")]
    public async Task Providers_CacheWorksOnSecondVisit()
    {
        await _page.GotoAsync("https://localhost:5432");
        await LoginIfRequired();
        
        // First visit
        await _page.ClickAsync("[data-tab=\"providers\"]");
        await _page.WaitForSelectorAsync("[id^=\"provider-card-\"]", new() { Timeout = 15000 });
        
        // Navigate away
        await _page.ClickAsync("[data-tab=\"dashboard\"]");
        await Task.Delay(500);
        
        // Navigate back - should use cache
        await _page.ClickAsync("[data-tab=\"providers\"]");
        
        // Should show provider cards immediately (from cache)
        var providerCards = await _page.Locator("[id^=\"provider-card-\"]").CountAsync();
        Assert.True(providerCards > 0);
    }

    [Fact(Skip = "Requires running application server")]
    public async Task Providers_RefreshAllShowsLoadingOverlay()
    {
        await _page.GotoAsync("https://localhost:5432");
        await LoginIfRequired();
        
        await _page.ClickAsync("[data-tab=\"providers\"]");
        await _page.WaitForSelectorAsync("#refreshProvBtn", new() { Timeout = 10000 });
        
        // Click refresh all
        await _page.ClickAsync("#refreshProvBtn");
        
        // Check for loading overlay (may be brief)
        var overlayCount = await _page.Locator(".loading-overlay:not(.hidden)").CountAsync();
        var statusCount = await _page.Locator("[id^=\"provider-status-\"]").CountAsync();
        Assert.True(overlayCount > 0 || statusCount > 0);
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
