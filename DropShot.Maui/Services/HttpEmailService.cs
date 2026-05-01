using System.Net;
using System.Net.Http.Json;
using DropShot.Shared.Dtos;
using DropShot.UI.Services;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI implementation of IEmailService. POSTs to /api/contact; the web project applies rate-limit
/// and email send. Returns false on HTTP 429 (rate-limited); throws on other failures.
/// </summary>
public sealed class HttpEmailService : IEmailService
{
    private readonly HttpClient _http;

    public HttpEmailService(HttpClient http) => _http = http;

    public async Task<bool> SendContactMessageAsync(ContactMessageDto message, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/contact", message, ct);
        if (response.StatusCode == HttpStatusCode.TooManyRequests) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }
}
