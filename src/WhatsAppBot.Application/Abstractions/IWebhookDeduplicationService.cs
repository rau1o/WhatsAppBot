namespace WhatsAppBot.Application.Abstractions;

// Puerto: Application solo sabe que puede "marcar un mensaje como ya
// procesado, y enterarse si ya lo había visto antes". No sabe que eso
// implica una tabla en la base — eso es Infrastructure.
public interface IWebhookDeduplicationService
{
    // Devuelve true la primera vez que ve este messageId (y lo registra).
    // Devuelve false si ya lo había visto — Meta reentregó el mismo mensaje.
    Task<bool> TryMarkAsProcessedAsync(string whatsAppMessageId, CancellationToken ct);
}
