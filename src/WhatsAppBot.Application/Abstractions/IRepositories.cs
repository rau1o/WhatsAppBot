using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Application.Abstractions;

public interface ITenantRepository
{
    Task<Tenant> GetByIdAsync(Guid id, CancellationToken ct);

    // clave para resolver el tenant desde el phone_number_id que manda Meta en el webhook
    Task<Tenant?> GetByWhatsAppPhoneNumberIdAsync(string phoneNumberId, CancellationToken ct);
    Task UpdateAsync(Tenant tenant, CancellationToken ct);
}

public interface IConversationRepository
{
    // Ya no recibe tenantId por parámetro: lo toma del ICurrentTenantAccessor
    // del scope actual. Así es imposible pasar por accidente el tenantId
    // equivocado en algún call site nuevo.
    Task<Conversation> GetOrCreateAsync(string customerPhoneNumber, CancellationToken ct);
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct);
    Task SaveAsync(Conversation conversation, CancellationToken ct);

    // Para el panel admin: lista las conversaciones del tenant actual,
    // más recientes primero.
    Task<IReadOnlyList<Conversation>> ListRecentAsync(CancellationToken ct);
}
public interface IOrderRepository
{
    // El pedido "borrador" es 1:1 con la conversación mientras el cliente
    // sigue agregando productos — por eso se resuelve por ConversationId,
    // no por un Id de pedido que todavía no existe del lado del cliente.
    Task<Order> GetOrCreateDraftAsync(Guid conversationId, CancellationToken ct);
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct);

    // Para adjuntar el comprobante: el pedido ya fue "Submitted" en
    // OrderReviewStateHandler, así que ya no es el draft — buscamos el
    // último pedido de la conversación sea cual sea su estado.
    // TODO: si en el futuro un mismo cliente hace pedidos repetidos en la
    // misma conversación, esto va a necesitar desambiguar por fecha o por
    // un Id de pedido explícito en vez de "el último".
    Task<Order?> GetLatestForConversationAsync(Guid conversationId, CancellationToken ct);

    // Para el panel admin: pedidos en preparación (aprobados, listos, o
    // entregados), del tenant actual. Excluye Draft/Submitted/Abandoned —
    // esos todavía no pasaron por la aprobación de un comprobante.
    Task<IReadOnlyList<Order>> ListByFulfillmentStatusAsync(OrderFulfillmentStatus status, CancellationToken ct);

    // Devuelve false si no se pudo aplicar el cambio (ej. conflicto de
    // concurrencia) — el caller NUNCA debe asumir éxito sin chequear esto.
    Task<bool> SaveAsync(Order order, CancellationToken ct);
}
