using System.Text.RegularExpressions;
using Azure.Communication.Email;

public class EmailService
{
    private readonly EmailClient? _emailClient;
    private readonly string? _sender;

    public EmailService(IConfiguration config)
    {
        var connectionString = config["ACS:ConnectionString"];
        if (string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("[Email] ACS:ConnectionString not configured. Email sending will be simulated.");
            return;
        }

        _sender = config["ACS:SenderAddress"] ?? throw new InvalidOperationException("ACS:SenderAddress not configured");
        _emailClient = new EmailClient(connectionString);
    }

    public async Task SendEmailAsync(string recipient, string subject, string body, bool isHtml = false)
    {
        if (_emailClient is null || _sender is null)
        {
            Console.WriteLine($"[Email] ACS not configured. Would send to {recipient}: {subject}");
            return;
        }

        EmailContent emailContent;

        if (isHtml)
        {
            emailContent = new EmailContent(subject)
            {
                Html = body,
                PlainText = StripHtmlForPlainText(body)
            };
        }
        else
        {
            emailContent = new EmailContent(subject)
            {
                PlainText = body,
                Html = $"<html><body><p>{System.Net.WebUtility.HtmlEncode(body)}</p></body></html>"
            };
        }

        var recipients = new EmailRecipients(new List<EmailAddress> { new EmailAddress(recipient) });

        var message = new EmailMessage(_sender, recipients, emailContent);

        await _emailClient.SendAsync(Azure.WaitUntil.Completed, message);
    }

    private static string StripHtmlForPlainText(string html)
    {
        var text = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"</p>", "\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", "");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }
}
