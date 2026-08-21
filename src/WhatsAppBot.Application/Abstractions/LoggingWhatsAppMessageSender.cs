using Microsoft.Extensions.Logging;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Application.Messaging;

namespace WhatsAppBot.Infrastructure.WhatsApp;

// Se usa automáticamente cuando WhatsAppCloudApi:AccessToken está vacío
// (ver DependencyInjection.cs) — así se puede debuggear todo el flujo de
// conversación sin credenciales reales de Meta. Nunca se registra si hay
// un AccessToken configurado.
public class LoggingWhatsAppMessageSender : IWhatsAppMessageSender
{
    private readonly ILogger<LoggingWhatsAppMessageSender> _logger;

    public LoggingWhatsAppMessageSender(ILogger<LoggingWhatsAppMessageSender> logger)
    {
        _logger = logger;
    }

    public Task SendTextAsync(string tenantPhoneNumberId, string toPhoneNumber, string text, CancellationToken ct)
    {
        _logger.LogInformation("[WHATSAPP DEV] → {To}: {Text}", toPhoneNumber, text);
        return Task.CompletedTask;
    }

    public Task SendLocationAsync(string tenantPhoneNumberId, string toPhoneNumber,
        double latitude, double longitude, string name, string address, CancellationToken ct)
    {
        _logger.LogInformation("[WHATSAPP DEV] → {To}: 📍 {Name} ({Lat}, {Lng}) — {Address}", toPhoneNumber, name, latitude, longitude, address);
        return Task.CompletedTask;
    }

    public Task SendImageByUrlAsync(string tenantPhoneNumberId, string toPhoneNumber, string imageUrl, string? caption, CancellationToken ct)
    {
        _logger.LogInformation("[WHATSAPP DEV] → {To}: 🖼️ {ImageUrl} ({Caption})", toPhoneNumber, imageUrl, caption);
        return Task.CompletedTask;
    }

    public Task SendInteractiveButtonsAsync(string tenantPhoneNumberId, string toPhoneNumber,
        string bodyText, IReadOnlyList<InteractiveButton> buttons, CancellationToken ct)
    {
        var buttonsText = string.Join(" | ", buttons.Select(b => $"[{b.Title}]({b.Id})"));
        _logger.LogInformation("[WHATSAPP DEV] → {To}: {Body}\nBotones: {Buttons}", toPhoneNumber, bodyText, buttonsText);
        return Task.CompletedTask;
    }

    public Task SendInteractiveListAsync(string tenantPhoneNumberId, string toPhoneNumber,
        string bodyText, string buttonText, IReadOnlyList<InteractiveListSection> sections, CancellationToken ct)
    {
        var rowsText = string.Join(" | ", sections.SelectMany(s => s.Rows).Select(r => $"[{r.Title}]({r.Id})"));
        _logger.LogInformation("[WHATSAPP DEV] → {To}: {Body}\nOpciones: {Rows}", toPhoneNumber, bodyText, rowsText);
        return Task.CompletedTask;
    }
}
