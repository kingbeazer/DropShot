using DropShot.E2E.Helpers;

namespace DropShot.E2E.AuthenticatedPages;

[TestFixture]
public class RulesSetsPageTests : DropShotPageTest
{
    [Test]
    public async Task RulesSetsPage_Requires_Authentication()
    {
        await AssertRequiresAuth("/rules");
    }

    [Test]
    public async Task RulesSetsPage_Loads_When_Authenticated()
    {
        var email = Environment.GetEnvironmentVariable("TEST_USER_EMAIL") ?? "test@example.com";
        var password = Environment.GetEnvironmentVariable("TEST_USER_PASSWORD") ?? "Test123!";

        await LoginAsync(email, password);
        await NavigateAndAssertLoaded("/rules");
    }
}
