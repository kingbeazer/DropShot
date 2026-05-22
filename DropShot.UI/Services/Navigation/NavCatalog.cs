using MudBlazor;

namespace DropShot.UI.Services.Navigation;

/// <summary>
/// Shared definition of a primary navigation link. The two hosts render
/// these in their own style (web: top-row anchor with sub-label; MAUI:
/// drawer's MudNavLink) but agree on the set of entries — adding or
/// removing a link in one place automatically updates both.
/// </summary>
/// <param name="Href">App-relative path, no leading slash. The hosts add
/// the slash where their renderer expects it.</param>
/// <param name="Label">Primary label shown to the user.</param>
/// <param name="Icon">MudBlazor icon SVG path string
/// (e.g. <c>Icons.Material.Filled.Gavel</c>).</param>
/// <param name="Sublabel">Optional secondary label shown by the web's
/// nav-sublabel slot. MAUI ignores it.</param>
/// <param name="RequiredRoles">Optional comma-separated roles. When
/// <c>null</c> the link is rendered for any authenticated user. When
/// empty the link is rendered for everyone (including anonymous).</param>
/// <param name="RequiresSubscription">When true the link is only shown to
/// users for whom <c>ICurrentUser.CanScoreMatch</c> is true — i.e. an
/// active subscriber or an admin/club-admin acting in their admin role.
/// Layered on top of <paramref name="RequiredRoles"/>: both must pass.</param>
public sealed record NavLinkEntry(
    string Href,
    string Label,
    string Icon,
    string? Sublabel = null,
    string? RequiredRoles = null,
    bool RequiresSubscription = false);

/// <summary>
/// Single source of truth for the navigation links shown in the web's
/// top-row navbar and the MAUI drawer.
///
/// <para><see cref="Primary"/> entries surface in the web's main toolbar
/// (Match for subscribers, Competitions for everyone). <see cref="Secondary"/>
/// entries are tucked into the web's right-hand account dropdown (Clubs,
/// Rules Sets, Players). MAUI flattens both lists into its drawer since the
/// drawer has no equivalent toolbar/dropdown split.</para>
///
/// Host-specific entries (web's My-Players-vs-Club-Players toggle, MAUI's
/// Home shortcut, account / admin / theme sections) stay in their respective
/// <c>NavMenu.razor</c> because they need bespoke rendering.
/// </summary>
public static class NavCatalog
{
    public static readonly IReadOnlyList<NavLinkEntry> Primary =
    [
        new("match", "Match",
            Icons.Material.Filled.SportsTennis,
            "Score or join a match",
            RequiredRoles: "User",
            RequiresSubscription: true),

        new("competitions", "Competitions",
            Icons.Material.Filled.EmojiEvents,
            "Browse and manage events"),
    ];

    public static readonly IReadOnlyList<NavLinkEntry> Secondary =
    [
        new("clubs", "Clubs",
            Icons.Material.Filled.Business,
            "Browse tennis clubs"),

        new("rulessets", "Rules Sets",
            Icons.Material.Filled.Gavel,
            "Match rules templates",
            // Plain users don't manage or browse rules-set templates —
            // those are an admin / club-admin concern, mirrored on the
            // /rulessets page itself where Edit / Rules / Delete are
            // already CurrentUser.IsAdmin gated.
            RequiredRoles: "ClubAdmin,Admin,SuperAdmin"),

        new("players", "Players",
            Icons.Material.Filled.People,
            "All players in the system",
            RequiredRoles: "SuperAdmin"),
    ];
}
