using DropShot.E2E.Helpers;

namespace DropShot.E2E.AdminPages;

[TestFixture]
public class UserManagementTests : DropShotPageTest
{
    [Test]
    public async Task UserManagementPage_Requires_Authentication()
    {
        await AssertRequiresAuth("/admin/users");
    }

    [Test]
    public async Task UserManagementPage_Loads_For_Admin()
    {
        var email = Environment.GetEnvironmentVariable("TEST_ADMIN_EMAIL") ?? "admin@example.com";
        var password = Environment.GetEnvironmentVariable("TEST_ADMIN_PASSWORD") ?? "Admin123!";

        await LoginAsync(email, password);
        await NavigateAndAssertLoaded("/admin/users", "User Management");
    }
}
