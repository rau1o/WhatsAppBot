using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Contracts;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Application.Messaging;
using WhatsAppBot.Infrastructure.WhatsApp;

namespace WhatsAppBot.Api.Controllers;

[ApiController]
[Route("api/webhook/whatsapp")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly WhatsAppCloudApiOptions _options;
    private readonly ITenantRepository _tenants;
    private readonly IBackgroundJobEnqueuer _jobs;
    private readonly IWebhookDeduplicationService _deduplication;

    public WhatsAppWebhookController(
        IOptions<WhatsAppCloudApiOptions> options,
        ITenantRepository tenants,
        IBackgroundJobEnqueuer jobs,
        IWebhookDeduplicationService deduplication)
    {
        _options = options.Value;
        _tenants = tenants;
        _jobs = jobs;
        _deduplication = deduplication;
    }

    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.challenge")] string challenge,
        [FromQuery(Name = "hub.verify_token")] string token)
    {
        if (token != _options.VerifyToken) return Forbid();

        // Content(), no Ok(): con [ApiController], Ok(string) pasa por el
        // negociador de contenido y lo serializa como JSON (queda con
        // comillas, "12345" en vez de 12345). Meta compara el body exacto
        // contra el challenge que mandó — con comillas de más, siempre falla.
        return Content(challenge, "text/plain");
    }

    [HttpPost]    
    public async Task<IActionResult> Receive([FromBody] WhatsAppWebhookPayload payload, CancellationToken ct)
    {
        var value = payload.Entry.FirstOrDefault()?.Changes.FirstOrDefault()?.Value;
        var message = value?.Messages?.FirstOrDefault();

        if (value is null || message is null)
            return Ok(); // notificación sin mensaje (ej. status update) — se ignora

        var tenant = await _tenants.GetByWhatsAppPhoneNumberIdAsync(value.Metadata.PhoneNumberId, ct);
        if (tenant is null) return Ok(); // número no registrado en ningún tenant
                                         
        // Meta puede reentregar el mismo mensaje (por ejemplo, si nuestra
        // respuesta tardó más de la cuenta esa vez puntual — típico con una
        // conexión a la base "fría" tras un rato sin actividad). Sin este
        // chequeo, el reintento se procesaría como un mensaje nuevo: productos
        // duplicados, respuestas repetidas, etc.
        var isNewMessage = await _deduplication.TryMarkAsProcessedAsync(message.Id, ct);
        if (!isNewMessage) return Ok();

        var incoming = new IncomingMessage(
            CustomerPhoneNumber: message.From,
            TenantPhoneNumberId: value.Metadata.PhoneNumberId,
            Text: message.Text?.Body,
            InteractiveButtonId: message.Interactive?.ButtonReply?.Id,
            ListReplyId: message.Interactive?.ListReply?.Id,
            MediaId: message.Image?.Id
        );

        // El webhook responde 200 de inmediato — Meta reintenta si no
        // contestás rápido. El procesamiento real corre en background.
        _jobs.EnqueueProcessMessage(tenant.Id, incoming);

        return Ok();
    }
}
