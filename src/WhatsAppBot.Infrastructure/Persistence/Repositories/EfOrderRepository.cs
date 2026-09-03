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
    public async Task<IReadOnlyList<Order>> ListByFulfillmentStatusAsync(OrderFulfillmentStatus status, CancellationToken ct)
    {
        RequireTenantId();

        return await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.FulfillmentStatus == status)
            .OrderBy(o => o.CreatedAt) // los más viejos primero — son los que llevan más tiempo esperando
            .ToListAsync(ct);
    }
    public async Task<IReadOnlyList<Order>> ListPaidOrdersInRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        RequireTenantId();

        return await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.FulfillmentStatus != null && o.CreatedAt >= fromUtc && o.CreatedAt <= toUtc)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(ct);
    }
    public async Task<bool> SaveAsync(Order order, CancellationToken ct)
    {
        // OrderItem usa una clave (Guid) generada por nuestro código, no por
        // la base — cuando EF descubre una entidad nueva por su cuenta (vía
        // auto-detect recorriendo la colección Items de un Order ya
        // trackeado, en vez de un Add() explícito) y esa entidad ya tiene
        // una clave con valor "real" asignado, EF asume por default que ya
        // existe en la base y la marca Modified en vez de Added — generando
        // un UPDATE contra una fila que nunca existió.
        //
        // El problema con solo chequear el Estado después: llamar a
        // _db.Entry(x) en CUALQUIER item dispara una detección de cambios de
        // TODO el contexto — así que con un foreach normal, el auto-detect
        // "descubre" y clasifica mal al item nuevo ANTES de que el bucle
        // llegue a revisarlo. Por eso desactivamos el auto-detect mientras
        // clasificamos nosotros mismos qué es genuinamente nuevo.
        var wasAutoDetectEnabled = _db.ChangeTracker.AutoDetectChangesEnabled;
        _db.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            var trackedItemIds = _db.ChangeTracker.Entries<OrderItem>()
                .Select(e => e.Entity.Id)
                .ToHashSet();

            foreach (var item in order.Items)
            {
                if (!trackedItemIds.Contains(item.Id))
                {
                    _db.Set<OrderItem>().Add(item);
                }
            }
        }
        finally
        {
            // Reactivamos el auto-detect para que SÍ se detecten cambios
            // legítimos en items que ya existían (ej. Quantity incrementada
            // al elegir el mismo producto dos veces).
            _db.ChangeTracker.AutoDetectChangesEnabled = wasAutoDetectEnabled;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Detalle completo de la entidad en conflicto — no solo la clave,
            // así vemos contra qué OrderId/ProductId está chocando de verdad
            // en vez de tener que adivinar.
            var affectedEntries = ex.Entries.Select(e => e.Entity switch
            {
                OrderItem oi => $"OrderItem(State={e.State}, Id={oi.Id}, OrderId={oi.OrderId}, ProductId={oi.ProductId}, ProductName={oi.ProductName}, Quantity={oi.Quantity})",
                Order o2 => $"Order(State={e.State}, Id={o2.Id}, Status={o2.Status})",
                _ => $"{e.Entity.GetType().Name}(State={e.State})"
            }).ToList();

            _logger.LogWarning(ex,
                "Concurrencia al guardar el pedido {OrderId} (con items actuales: {CurrentItems}) — el cambio NO se aplicó. Entidades en conflicto: {Entries}",
                order.Id,
                string.Join(" | ", order.Items.Select(i => $"{i.ProductName}(Id={i.Id}, ProductId={i.ProductId}, Qty={i.Quantity})")),
                string.Join(" | ", affectedEntries));

            // Crítico: sin esto, el DbContext queda en un estado inconsistente
            // después del SaveChangesAsync fallido, y CUALQUIER operación
            // posterior en el mismo DbContext (ej. guardar la conversación al
            // final de MessageProcessor) puede fallar con un error que no
            // tiene nada que ver. El resto del job comparte esta misma
            // instancia de DbContext, así que hay que dejarla limpia.
            _db.ChangeTracker.Clear();

            return false;
        }

    }

    private Guid RequireTenantId()
        => _currentTenant.TenantId
           ?? throw new InvalidOperationException("No hay un tenant actual seteado en este scope.");
}
