using WhatsAppBot.Application.Messaging;

namespace WhatsAppBot.Application.Abstractions;

// Puerto: Application define QUÉ necesita, Infrastructure decide CÓMO
// (HttpClient contra la Cloud API de Meta). Application nunca sabe
// que del otro lado hay HTTP.
public interface IWhatsAppMessageSender
{
    Task SendTextAsync(string tenantPhoneNumberId, string toPhoneNumber, string text, CancellationToken ct);

    Task SendLocationAsync(string tenantPhoneNumberId, string toPhoneNumber,
        double latitude, double longitude, string name, string address, CancellationToken ct);

    Task SendImageByUrlAsync(string tenantPhoneNumberId, string toPhoneNumber, string imageUrl, string? caption, CancellationToken ct);

    // Máximo 3 botones — límite de la Cloud API de Meta.
    Task SendInteractiveButtonsAsync(string tenantPhoneNumberId, string toPhoneNumber,
        string bodyText, IReadOnlyList<InteractiveButton> buttons, CancellationToken ct);

    // Máximo 10 filas en total entre todas las secciones — límite de la Cloud API de Meta.
    Task SendInteractiveListAsync(string tenantPhoneNumberId, string toPhoneNumber,
        string bodyText, string buttonText, IReadOnlyList<InteractiveListSection> sections, CancellationToken ct);
}
