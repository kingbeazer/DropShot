using System.Collections.Concurrent;
using DropShot.Models;

namespace DropShot.Services;

public class QrLoginService
{
    private readonly ConcurrentDictionary<string, QrLoginSession> _sessions = new();

    public QrLoginSession GenerateSession()
    {
        var session = new QrLoginSession { Token = Guid.NewGuid().ToString("N") };
        _sessions[session.Token] = session;
        return session;
    }

    public QrLoginSession? GetSession(string token)
    {
        if (!_sessions.TryGetValue(token, out var session))
            return null;

        if (session.IsExpired && session.Status == QrSessionStatus.Pending)
        {
            session.Status = QrSessionStatus.Expired;
        }

        return session;
    }

    public bool ConfirmSession(string token, string userId, string userName, List<string> roles, int? courtId)
    {
        if (!_sessions.TryGetValue(token, out var session))
            return false;

        if (session.Status != QrSessionStatus.Pending || session.IsExpired)
            return false;

        session.UserId = userId;
        session.UserName = userName;
        session.Roles = roles;
        session.CourtId = courtId;
        session.Status = QrSessionStatus.Authenticated;
        return true;
    }

    public void RemoveSession(string token)
    {
        _sessions.TryRemove(token, out _);
    }

    public void CleanupExpiredSessions()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-6); // keep a bit longer than 5 min for status checks
        foreach (var kvp in _sessions)
        {
            if (kvp.Value.CreatedAt < cutoff)
                _sessions.TryRemove(kvp.Key, out _);
        }
    }
}
