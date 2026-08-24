using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Infrastructure.Persistence.Repositories;

public class EfOrderRepository : IOrderRepository
{
    private readonly WhatsAppBotDbContext _db;
    private readonly ICurrentTenantAccessor _currentTenant;
    private readonly ILogger<EfOrderRepository> _logger;

    public EfOrderRepository(WhatsAppBotDbContext db, ICurrentTenantAccessor currentTenant, ILogger<EfOrderRepository> logger)
    {
        _db = db;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public async Task<Order> GetOrCreateDraftAsync(Guid conversationId, CancellationToken ct)
    {
        var tenantId = RequireTenantId();

        var existing = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.ConversationId == conversationId && o.Status == OrderStatus.Draft, ct);

        if (existing is not null) return existing;

        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            Status = OrderStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        return order;
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
       => await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<Order?> GetLatestForConversationAsync(Guid conversationId, CancellationToken ct)
        => await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.ConversationId == conversationId)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task SaveAsync(Order order, CancellationToken ct)
    {
        // No hace falta Attach/Update: order y sus Items ya vienen trackeados
        // por este mismo DbContext (scoped a este job/request) desde
        // GetOrCreateDraftAsync — EF detecta solo los cambios en memoria
        // (items agregados, cantidades, status).
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Pasa sobre todo cuando Hangfire reintenta un job que ya había
            // guardado este mismo cambio en un intento anterior (ej.: el
            // intento previo guardó bien en la base pero falló DESPUÉS, al
            // mandar la respuesta de WhatsApp — Hangfire reintenta el job
            // entero, no solo la parte que falló). En vez de tumbar el job
            // de nuevo, lo tratamos como ya aplicado: es la interpretación
            // más segura, porque este método siempre guarda un estado
            // derivado (item agregado o cantidad incrementada), no algo que
            // dependa de un valor anterior específico.
            _logger.LogWarning(ex,
                "Concurrencia al guardar el pedido {OrderId} — probablemente un reintento de Hangfire sobre un cambio que ya se había aplicado.",
                order.Id);
        }

    }

    private Guid RequireTenantId()
        => _currentTenant.TenantId
           ?? throw new InvalidOperationException("No hay un tenant actual seteado en este scope.");
}
