using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Application.Messaging;

namespace WhatsAppBot.Infrastructure.WhatsApp;

// Implementación concreta de IWhatsAppMessageSender contra
// https://graph.facebook.com/v20.0/{phone_number_id}/messages
public class WhatsAppCloudApiSender : IWhatsAppMessageSender
{
    private readonly HttpClient _http;
    private readonly WhatsAppCloudApiOptions _options;

    public WhatsAppCloudApiSender(HttpClient http, IOptions<WhatsAppCloudApiOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public Task SendTextAsync(string tenantPhoneNumberId, string toPhoneNumber, string text, CancellationToken ct)
        => PostAsync(tenantPhoneNumberId, new
        {
            messaging_product = "whatsapp",
            to = toPhoneNumber,
            type = "text",
            text = new { body = text }
        }, ct);

    public Task SendLocationAsync(string tenantPhoneNumberId, string toPhoneNumber,
        double latitude, double longitude, string name, string address, CancellationToken ct)
        => PostAsync(tenantPhoneNumberId, new
        {
            messaging_product = "whatsapp",
            to = toPhoneNumber,
            type = "location",
            location = new { latitude, longitude, name, address }
        }, ct);

    public Task SendImageByUrlAsync(string tenantPhoneNumberId, string toPhoneNumber, string imageUrl, string? caption, CancellationToken ct)
        => PostAsync(tenantPhoneNumberId, new
        {
            messaging_product = "whatsapp",
            to = toPhoneNumber,
            type = "image",
            image = new { link = imageUrl, caption }
        }, ct);

    public Task SendInteractiveButtonsAsync(string tenantPhoneNumberId, string toPhoneNumber,
        string bodyText, IReadOnlyList<InteractiveButton> buttons, CancellationToken ct)
    {
        if (buttons.Count > 3)
            throw new ArgumentException("WhatsApp permite máximo 3 botones de respuesta rápida.", nameof(buttons));

        return PostAsync(tenantPhoneNumberId, new
        {
            messaging_product = "whatsapp",
            to = toPhoneNumber,
            type = "interactive",
            interactive = new
            {
                type = "button",
                body = new { text = bodyText },
                action = new
                {
                    buttons = buttons.Select(b => new
                    {
                        type = "reply",
                        reply = new { id = b.Id, title = b.Title }
                    })
                }
            }
        }, ct);
    }

    public Task SendInteractiveListAsync(string tenantPhoneNumberId, string toPhoneNumber,
        string bodyText, string buttonText, IReadOnlyList<InteractiveListSection> sections, CancellationToken ct)
    {
        var totalRows = sections.Sum(s => s.Rows.Count);
        if (totalRows > 10)
            throw new ArgumentException("WhatsApp permite máximo 10 filas en total entre todas las secciones.", nameof(sections));

        return PostAsync(tenantPhoneNumberId, new
        {
            messaging_product = "whatsapp",
            to = toPhoneNumber,
            type = "interactive",
            interactive = new
            {
                type = "list",
                body = new { text = bodyText },
                action = new
                {
                    button = buttonText,
                    sections = sections.Select(s => new
                    {
                        title = s.Title,
                        rows = s.Rows.Select(r => new { id = r.Id, title = r.Title, description = r.Description })
                    })
                }
            }
        }, ct);
    }

    private async Task PostAsync(string tenantPhoneNumberId, object payload, CancellationToken ct)
    {
        var url = $"{_options.BaseUrl}/{tenantPhoneNumberId}/messages";

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new("Bearer", _options.AccessToken);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
