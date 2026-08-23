using Microsoft.Playwright;
using Xunit;

namespace AgentHub.IntegrationTests.Web.Playwright;

[Collection("PlaywrightCollection")]
public class DashboardTests(PlaywrightTestFixture fixture)
{
    private readonly PlaywrightTestFixture _fixture = fixture;

    [Fact]
    public async Task Dashboard_ShowsSkeletonsOrStatsOnFirstLoad()
    {
        var page = await _fixture.CreatePageAsync();
        try
        {
            _ = await page.GotoAsync(_fixture.ServerAddress);
            await PlaywrightTestHelper.LoginIfRequiredAsync(page);

            await page.ClickAsync("[data-tab=\"dashboard\"]");
            _ = await page.WaitForSelectorAsync(".stat-val", new PageWaitForSelectorOptions { Timeout = 10000 });

            var statValCount = await page.Locator(".stat-val").CountAsync();
            Assert.True(statValCount >= 3);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task Dashboard_ShowsLastUpdatedTimestamp()
    {
        var page = await _fixture.CreatePageAsync();
        try
        {
            _ = await page.GotoAsync(_fixture.ServerAddress);
            await PlaywrightTestHelper.LoginIfRequiredAsync(page);

            await page.ClickAsync("[data-tab=\"dashboard\"]");
            _ = await page.WaitForSelectorAsync(".last-updated", new PageWaitForSelectorOptions { Timeout = 10000 });

            var lastUpdated = await page.TextContentAsync(".last-updated");
            Assert.NotNull(lastUpdated);
            Assert.Contains("Updated", lastUpdated);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task Dashboard_CacheWorksOnSecondVisit()
    {
        var page = await _fixture.CreatePageAsync();
        try
        {
            _ = await page.GotoAsync(_fixture.ServerAddress);
            await PlaywrightTestHelper.LoginIfRequiredAsync(page);

            // First visit - fetch data
            await page.ClickAsync("[data-tab=\"dashboard\"]");
            _ = await page.WaitForSelectorAsync(".stat-val", new PageWaitForSelectorOptions { Timeout = 10000 });

            // Navigate away
            await page.ClickAsync("[data-tab=\"providers\"]");
            await Task.Delay(300);

            // Navigate back
            await page.ClickAsync("[data-tab=\"dashboard\"]");
            _ = await page.WaitForSelectorAsync(".stat-val", new PageWaitForSelectorOptions { Timeout = 10000 });

            var statVals = await page.Locator(".stat-val").CountAsync();
            Assert.True(statVals >= 3);
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
