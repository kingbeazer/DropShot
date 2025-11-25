using Azure.Communication.Email;

public class EmailService
{
    private readonly EmailClient _emailClient;
    private readonly string _sender;

    public EmailService(IConfiguration config)
    {
        var connectionString = config["ACS:ConnectionString"];
        _sender = config["ACS:SenderAddress"];
        _emailClient = new EmailClient(connectionString);
    }

    public async Task SendEmailAsync(string recipient, string subject, string body)
    {
        var emailContent = new EmailContent(subject)
        {
            PlainText = body,
            Html = $"<html><body><p>{body}</p></body></html>"
        };

        var recipients = new EmailRecipients(new List<EmailAddress> { new EmailAddress(recipient) });

        var message = new EmailMessage(_sender, recipients, emailContent);

        // Fix: Specify WaitUntil.Completed as required by the method signature
        await _emailClient.SendAsync(Azure.WaitUntil.Completed, message);
    }
}
