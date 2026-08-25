using Microsoft.EntityFrameworkCore;
using WhatsAppBot.Application.Abstractions;

namespace WhatsAppBot.Infrastructure.Persistence.Repositories;

public class EfWebhookDeduplicationService : IWebhookDeduplicationService
{
    private readonly WhatsAppBotDbContext _db;

    public EfWebhookDeduplicationService(WhatsAppBotDbContext db)
    {
        _db = db;
    }

    public async Task<bool> TryMarkAsProcessedAsync(string whatsAppMessageId, CancellationToken ct)
    {
        var entry = new ProcessedWebhookMessage
        {
            WhatsAppMessageId = whatsAppMessageId,
            ProcessedAtUtc = DateTime.UtcNow
        };

        _db.ProcessedWebhookMessages.Add(entry);

        try
        {
            await _db.SaveChangesAsync(ct);
            return true; // primera vez que vemos este message_id
        }
        catch (DbUpdateException)
        {
            // La constraint única de la PK rechazó el insert — Meta ya nos
            // había mandado este mismo mensaje antes.
            _db.Entry(entry).State = EntityState.Detached;
            return false;
        }
    }
}
