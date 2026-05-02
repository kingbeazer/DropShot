namespace DropShot.Shared.Dtos;

/// <summary>
/// Court info needed by the scoreboard subcomponents (Default + Wimbledon
/// layouts) and the courts dropdown on the Scoreboard page.
/// </summary>
public record ScoreboardCourtDto(int CourtId, string ClubName, string CourtName);

/// <summary>
/// Active match info for scoreboard display: just the player labels (or
/// team-match home/away names) plus the start timestamp the Wimbledon
/// layout uses for the elapsed-time clock.
/// </summary>
public record ScoreboardActiveMatchDto(
    string? Player1,
    string? Player2,
    string? Player3,
    string? Player4,
    DateTime CreatedAt);

/// <summary>
/// Fixture metadata rendered alongside the scoreboard when an active
/// match is tied to a competition fixture (competition name + fixture label).
/// </summary>
public record ScoreboardFixtureDto(string? CompetitionName, string? FixtureLabel);

/// <summary>
/// Snapshot of a court's live scoreboard state: the in-progress active match
/// (if any), its linked fixture, the most recent <c>GameState</c> parsed from
/// <c>SavedMatch.MatchJson</c>, and the persisted per-court display settings
/// (layout / fullscreen / live-stream URL).
/// </summary>
public record ScoreboardCourtStateDto(
    GameState CurrentScore,
    ScoreboardActiveMatchDto? ActiveMatch,
    ScoreboardFixtureDto? ActiveFixture,
    ScoreboardDisplaySettingDto DisplaySetting);

public record ScoreboardDisplaySettingDto(
    int CourtId,
    string Layout,
    bool Fullscreen,
    string? LiveStreamUrl,
    bool ShowLiveStream);

public record UpdateDisplaySettingRequest(
    string? Layout = null,
    bool? Fullscreen = null,
    string? LiveStreamUrl = null,
    bool? ShowLiveStream = null);

/// <summary>
/// Combined courts + display-settings payload for the
/// <c>/score/display-control</c> admin page. Avoids the N+1 round-trips of
/// fetching courts + per-court state separately. Settings default to
/// <c>Layout="default"</c>, all switches off, when no row exists yet.
/// </summary>
public record AdminCourtDisplaySettingDto(
    int CourtId,
    string ClubName,
    string CourtName,
    string Layout,
    bool Fullscreen,
    string? LiveStreamUrl,
    bool ShowLiveStream);
