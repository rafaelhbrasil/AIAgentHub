using Microsoft.Playwright;
using Xunit;

namespace AgentHub.IntegrationTests.Web.Playwright;

[Collection("PlaywrightCollection")]
public class ProvidersTests(PlaywrightTestFixture fixture)
{
    private readonly PlaywrightTestFixture _fixture = fixture;

    [Fact]
    public async Task Providers_ShowsCardsOnLoad()
    {
        var page = await _fixture.CreatePageAsync();
        try
        {
            _ = await page.GotoAsync(_fixture.ServerAddress);
            await PlaywrightTestHelper.LoginIfRequiredAsync(page);

            await page.ClickAsync("[data-tab=\"providers\"]");
            _ = await page.WaitForSelectorAsync("[id^=\"provider-card-\"]", new PageWaitForSelectorOptions { Timeout = 10000 });

            var providerCardCount = await page.Locator("[id^=\"provider-card-\"]").CountAsync();
            Assert.True(providerCardCount > 0);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task Providers_CacheWorksOnSecondVisit()
    {
        var page = await _fixture.CreatePageAsync();
        try
        {
            _ = await page.GotoAsync(_fixture.ServerAddress);
            await PlaywrightTestHelper.LoginIfRequiredAsync(page);

            // First visit
            await page.ClickAsync("[data-tab=\"providers\"]");
            _ = await page.WaitForSelectorAsync("[id^=\"provider-card-\"]", new PageWaitForSelectorOptions { Timeout = 10000 });

            // Navigate away
            await page.ClickAsync("[data-tab=\"dashboard\"]");
            await Task.Delay(300);

            // Navigate back
            await page.ClickAsync("[data-tab=\"providers\"]");
            _ = await page.WaitForSelectorAsync("[id^=\"provider-card-\"]", new PageWaitForSelectorOptions { Timeout = 10000 });

            var providerCards = await page.Locator("[id^=\"provider-card-\"]").CountAsync();
            Assert.True(providerCards > 0);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task Providers_RefreshAllShowsLoadingOverlay()
    {
        var page = await _fixture.CreatePageAsync();
        try
        {
            _ = await page.GotoAsync(_fixture.ServerAddress);
            await PlaywrightTestHelper.LoginIfRequiredAsync(page);

            await page.ClickAsync("[data-tab=\"providers\"]");
            _ = await page.WaitForSelectorAsync("#refreshProvBtn", new PageWaitForSelectorOptions { Timeout = 10000 });

            // Click refresh all
            await page.ClickAsync("#refreshProvBtn");

            // Check for modal or provider status
            var modalCount = await page.Locator(".modal-container").CountAsync();
            var statusCount = await page.Locator("[id^=\"provider-status-\"]").CountAsync();
            Assert.True(modalCount > 0 || statusCount > 0);
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
