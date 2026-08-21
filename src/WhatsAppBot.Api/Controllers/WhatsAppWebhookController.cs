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

    public WhatsAppWebhookController(
        IOptions<WhatsAppCloudApiOptions> options,
        ITenantRepository tenants,
        IBackgroundJobEnqueuer jobs)
    {
        _options = options.Value;
        _tenants = tenants;
        _jobs = jobs;
    }

    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.challenge")] string challenge,
        [FromQuery(Name = "hub.verify_token")] string token)
    {
        if (token != _options.VerifyToken) return Forbid();
        return Ok(challenge);
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
