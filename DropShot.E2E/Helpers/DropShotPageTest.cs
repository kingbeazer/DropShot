using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace DropShot.E2E.Helpers;

public class DropShotPageTest : PageTest
{
    protected string BaseUrl =>
        Environment.GetEnvironmentVariable("PLAYWRIGHT_BASE_URL") ?? "https://localhost:5001";

    /// <summary>
    /// Login with the given credentials and wait for redirect.
    /// </summary>
    protected async Task LoginAsync(string email, string password)
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.FillAsync("input[name='Input.Email']", email);
        await Page.FillAsync("input[name='Input.Password']", password);
        await Page.ClickAsync("button[type='submit']");

        // Wait for redirect after login
        await Page.WaitForURLAsync(url => !url.Contains("/Account/Login"), new() { Timeout = 10000 });
    }

    /// <summary>
    /// Navigate to a path and assert the page loads with expected content.
    /// </summary>
    protected async Task NavigateAndAssertLoaded(string path, string? expectedText = null)
    {
        var response = await Page.GotoAsync($"{BaseUrl}{path}");
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.LessThan(500), $"Page {path} returned {response.Status}");

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        if (expectedText != null)
        {
            var content = await Page.ContentAsync();
            Assert.That(content, Does.Contain(expectedText),
                $"Page {path} does not contain expected text '{expectedText}'");
        }
    }

    /// <summary>
    /// Assert that navigating to a protected page without auth redirects to login.
    /// </summary>
    protected async Task AssertRequiresAuth(string path)
    {
        await Page.GotoAsync($"{BaseUrl}{path}");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var url = Page.Url;
        Assert.That(url, Does.Contain("/Account/Login"),
            $"Expected {path} to redirect to login, but URL is {url}");
    }
}
