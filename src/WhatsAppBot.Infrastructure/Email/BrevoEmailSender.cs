using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatsAppBot.Application.Abstractions;

namespace WhatsAppBot.Infrastructure.Email;

public class BrevoEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly BrevoOptions _options;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(IHttpClientFactory httpClientFactory, IOptions<BrevoOptions> options, ILogger<BrevoEmailSender> logger)
    {
        _http = httpClientFactory.CreateClient("Brevo");
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        var payload = new
        {
            sender = new { name = _options.SenderName, email = _options.SenderEmail },
            to = new[] { new { email = toEmail } },
            subject,
            htmlContent = htmlBody
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "smtp/email")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("api-key", _options.ApiKey);
        request.Headers.Add("Accept", "application/json");

        var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            // No relanzamos la excepción a propósito — que Brevo esté caído
            // no puede tumbar todo el flujo de "olvidé mi contraseña". El
            // caller le muestra igual el mensaje genérico de "revisá tu
            // email" al usuario (por diseño, para no filtrar qué emails
            // existen) — este log es la única forma de enterarse que en
            // realidad no salió.
            _logger.LogError("Brevo devolvió {StatusCode} al mandar un email a {ToEmail}: {Body}",
                response.StatusCode, toEmail, body);
        }
    }
}
