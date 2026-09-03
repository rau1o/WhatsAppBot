namespace WhatsAppBot.Application.Abstractions;

// Application solo sabe que puede "mandar un email". No sabe si eso es
// Brevo, SendGrid, SMTP directo, o lo que sea — eso es Infrastructure.
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct);
}
