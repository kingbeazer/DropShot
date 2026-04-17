namespace DropShot.Services;

public class EmailTemplateService
{
    private readonly string _baseUrl;
    private readonly string _logoUrl;

    public EmailTemplateService(IConfiguration config)
    {
        _baseUrl = config["App:BaseUrl"]?.TrimEnd('/') ?? "";
        _logoUrl = $"{_baseUrl}/Images/Logo.png";
    }

    // ── Base layout ──────────────────────────────────────────────────────────

    private string WrapInBaseLayout(string title, string bodyContentHtml, string? preheaderText = null)
    {
        var preheader = preheaderText != null
            ? $@"<span style=""display:none;font-size:1px;color:#f4f4f4;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;"">{Encode(preheaderText)}</span>"
            : "";

        return $@"<!DOCTYPE html>
<html lang=""en"" xmlns=""http://www.w3.org/1999/xhtml"" xmlns:v=""urn:schemas-microsoft-com:vml"" xmlns:o=""urn:schemas-microsoft-com:office:office"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"">
    <meta name=""color-scheme"" content=""light"">
    <meta name=""supported-color-schemes"" content=""light"">
    <title>{Encode(title)}</title>
    <!--[if mso]>
    <noscript>
        <xml>
            <o:OfficeDocumentSettings>
                <o:PixelsPerInch>96</o:PixelsPerInch>
            </o:OfficeDocumentSettings>
        </xml>
    </noscript>
    <![endif]-->
</head>
<body style=""margin:0;padding:0;background-color:#f4f4f4;font-family:'Helvetica Neue',Helvetica,Arial,sans-serif;"">
    {preheader}
    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color:#f4f4f4;"">
        <tr>
            <td align=""center"" style=""padding:24px 16px;"">
                <!--[if mso]><table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""600""><tr><td><![endif]-->
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""max-width:600px;background-color:#ffffff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08);"">
                    {HeaderRow()}
                    <tr>
                        <td style=""padding:32px 32px 24px 32px;color:#333333;font-size:16px;line-height:1.6;"">
                            {bodyContentHtml}
                        </td>
                    </tr>
                    {FooterRow()}
                </table>
                <!--[if mso]></td></tr></table><![endif]-->
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private string HeaderRow()
    {
        return $@"<tr>
    <td align=""center"" style=""background-color:#1b5e20;padding:24px 32px;"">
        <img src=""{_logoUrl}"" alt=""DropShot"" width=""140"" style=""display:block;border:0;outline:none;max-width:140px;height:auto;"">
    </td>
</tr>";
    }

    private static string FooterRow()
    {
        return $@"<tr>
    <td style=""background-color:#f9f9f9;padding:20px 32px;border-top:1px solid #eeeeee;"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"">
            <tr>
                <td align=""center"" style=""color:#999999;font-size:12px;line-height:1.5;"">
                    DropShot &mdash; Tennis Club Management<br>
                    &copy; {DateTime.UtcNow.Year} DropShot. All rights reserved.
                </td>
            </tr>
        </table>
    </td>
</tr>";
    }

    // ── Bulletproof CTA button ───────────────────────────────────────────────

    private static string ActionButton(string text, string url)
    {
        return $@"<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:24px auto;"">
    <tr>
        <td align=""center"" style=""border-radius:6px;background-color:#1565c0;"">
            <!--[if mso]>
            <v:roundrect xmlns:v=""urn:schemas-microsoft-com:vml"" xmlns:w=""urn:schemas-microsoft-com:office:word"" href=""{url}"" style=""height:48px;v-text-anchor:middle;width:240px;"" arcsize=""13%"" strokecolor=""#1565c0"" fillcolor=""#1565c0"">
            <w:anchorlock/>
            <center style=""color:#ffffff;font-family:'Helvetica Neue',Helvetica,Arial,sans-serif;font-size:16px;font-weight:bold;"">
                {Encode(text)}
            </center>
            </v:roundrect>
            <![endif]-->
            <!--[if !mso]><!-->
            <a href=""{url}"" target=""_blank"" style=""display:inline-block;padding:14px 32px;color:#ffffff;background-color:#1565c0;border-radius:6px;font-size:16px;font-weight:bold;text-decoration:none;text-align:center;line-height:1;"">
                {Encode(text)}
            </a>
            <!--<![endif]-->
        </td>
    </tr>
</table>";
    }

    private static string InfoBox(string contentHtml)
    {
        return $@"<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin:16px 0;"">
    <tr>
        <td style=""background-color:#f5f5f5;border-left:4px solid #1b5e20;padding:16px 20px;border-radius:0 4px 4px 0;font-size:15px;line-height:1.6;color:#333333;"">
            {contentHtml}
        </td>
    </tr>
</table>";
    }

    // ── Account emails ───────────────────────────────────────────────────────

    public string ConfirmationEmail(string callbackUrl)
    {
        var body = $@"
<h2 style=""margin:0 0 16px 0;font-size:22px;color:#1b5e20;"">Welcome to DropShot!</h2>
<p style=""margin:0 0 8px 0;"">Thanks for signing up. Please confirm your email address to get started.</p>
{ActionButton("Confirm Email Address", callbackUrl)}
<p style=""margin:16px 0 0 0;font-size:13px;color:#888888;"">If you didn't create a DropShot account, you can safely ignore this email.</p>";

        return WrapInBaseLayout("Confirm Your Email", body, "Please confirm your email address to activate your DropShot account.");
    }

    public string EmailChangeEmail(string callbackUrl)
    {
        var body = $@"
<h2 style=""margin:0 0 16px 0;font-size:22px;color:#1b5e20;"">Confirm Your New Email</h2>
<p style=""margin:0 0 8px 0;"">You've requested to change the email address on your DropShot account. Please confirm your new address by clicking the button below.</p>
{ActionButton("Confirm New Email", callbackUrl)}
<p style=""margin:16px 0 0 0;font-size:13px;color:#888888;"">If you didn't request this change, please ignore this email. Your account email will remain unchanged.</p>";

        return WrapInBaseLayout("Confirm Email Change", body, "Confirm your new email address for your DropShot account.");
    }

    public string PasswordResetEmail(string callbackUrl)
    {
        var body = $@"
<h2 style=""margin:0 0 16px 0;font-size:22px;color:#1b5e20;"">Reset Your Password</h2>
<p style=""margin:0 0 8px 0;"">We received a request to reset your DropShot password. Click the button below to choose a new one.</p>
{ActionButton("Reset Password", callbackUrl)}
<p style=""margin:16px 0 0 0;font-size:13px;color:#888888;"">If you didn't request a password reset, you can safely ignore this email. Your password will remain unchanged.</p>";

        return WrapInBaseLayout("Reset Your Password", body, "Reset your DropShot password.");
    }

    // ── Player invitation ────────────────────────────────────────────────────

    public string PlayerInvitationEmail(string playerName, string inviteLink)
    {
        var body = $@"
<h2 style=""margin:0 0 16px 0;font-size:22px;color:#1b5e20;"">You're Invited!</h2>
<p style=""margin:0 0 8px 0;"">You've been invited to join DropShot as <strong>{Encode(playerName)}</strong>.</p>
<p style=""margin:0 0 8px 0;"">Click the button below to register. Your new account will be linked to the existing player record and any match history will be transferred automatically.</p>
{ActionButton("Accept Invitation", inviteLink)}
<p style=""margin:16px 0 0 0;font-size:13px;color:#888888;"">If you weren't expecting this invitation, you can safely ignore it.</p>";

        return WrapInBaseLayout("DropShot Invitation", body, $"You've been invited to join DropShot as {playerName}.");
    }

    // ── Match / result emails ────────────────────────────────────────────────

    public string MatchResultEmail(string fixtureTitle, string resultSummary)
    {
        var body = $@"
<h2 style=""margin:0 0 16px 0;font-size:22px;color:#1b5e20;"">Match Result</h2>
<p style=""margin:0 0 12px 0;"">The result for your match has been recorded:</p>
{InfoBox($@"<strong>{Encode(fixtureTitle)}</strong><br>{Encode(resultSummary)}")}
<p style=""margin:16px 0 0 0;font-size:13px;color:#888888;"">This is an automated notification from DropShot.</p>";

        return WrapInBaseLayout($"Match Result: {fixtureTitle}", body, $"Result recorded: {resultSummary}");
    }

    public string AdminVerificationEmail(string fixtureTitle, string resultSummary, string verifyUrl)
    {
        var body = $@"
<h2 style=""margin:0 0 16px 0;font-size:22px;color:#1b5e20;"">Result Verification Required</h2>
<p style=""margin:0 0 12px 0;"">A result has been submitted and requires your verification:</p>
{InfoBox($@"<strong>{Encode(fixtureTitle)}</strong><br>{Encode(resultSummary)}")}
{ActionButton("Verify Result", verifyUrl)}
<p style=""margin:16px 0 0 0;font-size:13px;color:#888888;"">Please review and verify this result at your earliest convenience.</p>";

        return WrapInBaseLayout($"Verify Result: {fixtureTitle}", body, $"Result verification needed: {fixtureTitle}");
    }

    // ── Club link request emails ─────────────────────────────────────────────

    public string ClubLinkRequestReceivedEmail(string adminName, string requesterName, string clubName, string manageLink)
    {
        var body = $@"
<h2 style=""margin:0 0 16px 0;font-size:22px;color:#1b5e20;"">New Club Link Request</h2>
<p style=""margin:0 0 8px 0;"">Hi {Encode(adminName)},</p>
<p style=""margin:0 0 8px 0;""><strong>{Encode(requesterName)}</strong> has asked to be linked to <strong>{Encode(clubName)}</strong>.</p>
{ActionButton("Review Request", manageLink)}";

        return WrapInBaseLayout($"New club link request for {clubName}", body, $"{requesterName} wants to join {clubName}.");
    }

    public string ClubLinkRequestApprovedEmail(string userName, string clubName, string clubLink)
    {
        var body = $@"
<h2 style=""margin:0 0 16px 0;font-size:22px;color:#1b5e20;"">Request Approved</h2>
<p style=""margin:0 0 8px 0;"">Hi {Encode(userName)},</p>
<p style=""margin:0 0 8px 0;"">Your request to join <strong>{Encode(clubName)}</strong> has been approved. You're now linked to the club.</p>
{ActionButton("View Club", clubLink)}";

        return WrapInBaseLayout($"You're now linked to {clubName}", body, $"Your request to join {clubName} was approved.");
    }

    public string ClubLinkRequestRejectedEmail(string userName, string clubName)
    {
        var body = $@"
<h2 style=""margin:0 0 16px 0;font-size:22px;color:#1b5e20;"">Request Declined</h2>
<p style=""margin:0 0 8px 0;"">Hi {Encode(userName)},</p>
<p style=""margin:0 0 8px 0;"">Unfortunately, your request to join <strong>{Encode(clubName)}</strong> was not approved.</p>
<p style=""margin:0 0 8px 0;"">If you think this is a mistake, please reach out to the club directly.</p>";

        return WrapInBaseLayout("Club link request declined", body, $"Your request to join {clubName} was declined.");
    }

    // ── Admin custom email (wraps admin-authored text in branded layout) ──

    /// <summary>
    /// Wraps admin-authored text in the branded layout.
    /// The bodyText should already have variable values HTML-encoded (via SubstituteVariables).
    /// Line breaks in the text are converted to &lt;br&gt; tags.
    /// </summary>
    public string AdminCustomEmail(string bodyText)
    {
        var htmlBody = bodyText.Replace("\n", "<br>");

        var body = $@"
<div style=""font-size:16px;line-height:1.6;color:#333333;"">
    {htmlBody}
</div>";

        return WrapInBaseLayout("DropShot", body);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string Encode(string text) => System.Net.WebUtility.HtmlEncode(text);
}
