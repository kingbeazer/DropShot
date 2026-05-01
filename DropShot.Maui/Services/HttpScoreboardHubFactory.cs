using DropShot.UI.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI implementation of <see cref="IScoreboardHubFactory"/>. Builds the
/// connection against an absolute URL (<c>App:BaseUrl</c> + <c>/chathub</c>)
/// and attaches the JWT bearer token via <c>AccessTokenProvider</c> so the
/// hub authentication picks up the signed-in MAUI user.
/// </summary>
public sealed class HttpScoreboardHubFactory(
    IConfiguration config,
    AuthService auth) : IScoreboardHubFactory
{
    public HubConnection Create()
    {
        var baseUrl = config["App:BaseUrl"]?.TrimEnd('/') ?? "";
        var hubUrl = $"{baseUrl}/chathub";

        return new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () =>
                    Task.FromResult<string?>(auth.Session?.AccessToken);
            })
            .WithAutomaticReconnect()
            .Build();
    }
}
