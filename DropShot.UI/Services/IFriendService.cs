using DropShot.Shared.Dtos;

namespace DropShot.UI.Services;

/// <summary>
/// Friend relationships between the current user's Player and other Players
/// (<c>PlayerFriend</c>). Sending a request is also reachable via
/// <c>IMatchScoringService.SendFriendRequestAsync</c> (post-match "add
/// friend" prompt), which delegates here.
/// </summary>
public interface IFriendService
{
    Task<List<FriendDto>> GetFriendsAsync(CancellationToken ct = default);
    Task<List<FriendRequestDto>> GetIncomingRequestsAsync(CancellationToken ct = default);
    Task<List<FriendRequestDto>> GetOutgoingRequestsAsync(CancellationToken ct = default);

    /// <summary>Search verified (non-light) players by display name, excluding the current user.</summary>
    Task<List<FriendSearchResultDto>> SearchPlayersAsync(string term, CancellationToken ct = default);

    /// <summary>
    /// Inserts a Pending PlayerFriend row from the current user's Player to
    /// <paramref name="targetPlayerId"/>, unless one already exists in either
    /// direction. No-op when there's no current user or no linked Player.
    /// </summary>
    Task SendFriendRequestAsync(int targetPlayerId, CancellationToken ct = default);

    Task AcceptFriendRequestAsync(int requestingPlayerId, CancellationToken ct = default);
    Task RejectFriendRequestAsync(int requestingPlayerId, CancellationToken ct = default);
    Task RemoveFriendAsync(int friendPlayerId, CancellationToken ct = default);

    /// <summary>
    /// True when any Player linked to <paramref name="userAId"/> has an
    /// Accepted PlayerFriend relationship with any Player linked to
    /// <paramref name="userBId"/>.
    /// </summary>
    Task<bool> AreUsersFriendsAsync(string userAId, string userBId, CancellationToken ct = default);
}
