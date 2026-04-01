using Xunit;

namespace DropShot.Tests.Account;

/// <summary>
/// Account pages (Login, Register, etc.) use SSR with HttpContext and internal
/// IdentityRedirectManager which cannot be mocked in bUnit.
/// These pages are covered by the Playwright E2E tests instead.
/// This placeholder class documents that decision.
/// </summary>
public class LoginTests
{
    [Fact]
    public void AccountPages_Are_Covered_By_E2E_Tests()
    {
        // SSR account pages require HttpContext and IdentityRedirectManager
        // which are not available in bUnit's in-memory rendering.
        // See DropShot.E2E project for browser-based login page tests.
        Assert.True(true);
    }
}
