namespace WhatsAppBot.Infrastructure.Persistence;

// A propósito NO vive en Domain: es un detalle técnico de "ya vimos este
// webhook", no un concepto de negocio. Sin tenant/global filter — el
// message_id de WhatsApp es único globalmente, no por tenant.
public class ProcessedWebhookMessage
{
    public string WhatsAppMessageId { get; set; } = default!;
    public DateTime ProcessedAtUtc { get; set; }
}
