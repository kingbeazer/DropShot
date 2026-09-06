using DropShot.UI.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace DropShot.Maui.Services;

/// <summary>
/// MAUI implementation of <see cref="IMessagingHubFactory"/>. Builds the
/// connection against an absolute URL (<c>App:BaseUrl</c> + <c>/messaginghub</c>)
/// and attaches the JWT bearer token via <c>AccessTokenProvider</c> so the
/// hub authentication picks up the signed-in MAUI user.
/// </summary>
public sealed class HttpMessagingHubFactory(
    IConfiguration config,
    AuthService auth) : IMessagingHubFactory
{
    public HubConnection Create()
    {
        var baseUrl = config["App:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
            baseUrl = MauiProgram.ApiBaseUrl.TrimEnd('/');
        var hubUrl = $"{baseUrl}/messaginghub";

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
