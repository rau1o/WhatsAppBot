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

    public async Task<bool> SaveAsync(Order order, CancellationToken ct)
    {
        // OrderItem usa un Guid generado por nuestro código (Order.AddOrIncrementItem),
        // no por la base de datos — cuando se agrega a Items vía List.Add() en vez
        // de _db.Set<OrderItem>().Add(), EF no puede deducir solo por el valor de la
        // clave si es un registro nuevo o uno existente, y por default genera un
        // UPDATE (no un INSERT) para cualquier entidad con clave ya asignada que no
        // esté explícitamente trackeada. Sin esto, un item nuevo termina como un
        // UPDATE contra una fila que no existe → DbUpdateConcurrencyException con
        // "0 rows affected", que es justo lo que estábamos viendo.
        foreach (var item in order.Items)
        {
            if (_db.Entry(item).State == EntityState.Detached)
            {
                _db.Entry(item).State = EntityState.Added;
            }
        }

        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // ex.Entries nos dice EXACTAMENTE qué entidad(es) estaban en el
            // batch que falló y en qué estado — sin esto, "0 rows affected"
            // no alcanza para saber si el problema es el Order o algún OrderItem.
            var affectedEntries = ex.Entries
                .Select(e => $"{e.Entity.GetType().Name}(State={e.State}, Key={string.Join(",", e.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue))})")
                .ToList();

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
