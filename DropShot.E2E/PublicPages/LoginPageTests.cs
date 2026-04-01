using DropShot.E2E.Helpers;

namespace DropShot.E2E.PublicPages;

[TestFixture]
public class LoginPageTests : DropShotPageTest
{
    [Test]
    public async Task LoginPage_Loads_Successfully()
    {
        await NavigateAndAssertLoaded("/Account/Login", "Log in");
    }

    [Test]
    public async Task LoginPage_Has_Email_And_Password_Fields()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var emailInput = Page.Locator("input[name='Input.Email']");
        var passwordInput = Page.Locator("input[name='Input.Password']");

        await Expect(emailInput).ToBeVisibleAsync();
        await Expect(passwordInput).ToBeVisibleAsync();
    }
}
