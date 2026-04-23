using DropShot.Data;
using DropShot.Models;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Services;

/// <summary>
/// Admin-tunable, app-wide settings that live in the AppSettings key/value
/// table. Values are cached in memory and only re-read on a write.
/// </summary>
public sealed class SiteSettingsService
{
    public const string ContentMaxWidthPxKey = "ContentMaxWidthPx";
    public const int ContentMaxWidthPxDefault = 1280;
    public const int ContentMaxWidthPxMin = 960;
    public const int ContentMaxWidthPxMax = 2400;

    private readonly IDbContextFactory<MyDbContext> _dbFactory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private int? _contentMaxWidthPx;

    public SiteSettingsService(IDbContextFactory<MyDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<int> GetContentMaxWidthPxAsync(CancellationToken ct = default)
    {
        if (_contentMaxWidthPx is { } cached) return cached;

        await _lock.WaitAsync(ct);
        try
        {
            if (_contentMaxWidthPx is { } cached2) return cached2;

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var raw = await db.AppSettings
                .Where(s => s.Setting == ContentMaxWidthPxKey)
                .Select(s => s.Value)
                .FirstOrDefaultAsync(ct);

            var value = ParseWidth(raw) ?? ContentMaxWidthPxDefault;
            _contentMaxWidthPx = value;
            return value;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetContentMaxWidthPxAsync(int px, CancellationToken ct = default)
    {
        if (px < ContentMaxWidthPxMin || px > ContentMaxWidthPxMax)
            throw new ArgumentOutOfRangeException(nameof(px),
                $"Content width must be between {ContentMaxWidthPxMin} and {ContentMaxWidthPxMax} px.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Setting == ContentMaxWidthPxKey, ct);
        if (row is null)
        {
            row = new AppSetting { Setting = ContentMaxWidthPxKey, Value = px.ToString() };
            db.AppSettings.Add(row);
        }
        else
        {
            row.Value = px.ToString();
        }
        await db.SaveChangesAsync(ct);

        await _lock.WaitAsync(ct);
        try { _contentMaxWidthPx = px; }
        finally { _lock.Release(); }
    }

    private static int? ParseWidth(string? raw) =>
        int.TryParse(raw, out var n) && n >= ContentMaxWidthPxMin && n <= ContentMaxWidthPxMax
            ? n
            : (int?)null;
}
