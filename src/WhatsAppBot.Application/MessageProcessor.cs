using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Application.Messaging;
using WhatsAppBot.Application.StateHandlers;

namespace WhatsAppBot.Application;

// Se ejecuta en background (Hangfire) después de que el webhook encola
// el mensaje. Carga la conversación, delega en el handler del estado
// actual, y persiste el nuevo estado.
public class MessageProcessor : IMessageProcessor
{
    private readonly StateHandlerResolver _resolver;
    private readonly ITenantRepository _tenants;
    private readonly IConversationRepository _conversations;
    private readonly ICurrentTenantAccessor _currentTenant;

    public MessageProcessor(
        StateHandlerResolver resolver,
        ITenantRepository tenants,
        IConversationRepository conversations,
        ICurrentTenantAccessor currentTenant)
    {
        _resolver = resolver;
        _tenants = tenants;
        _conversations = conversations;
        _currentTenant = currentTenant;
    }

    public async Task ProcessAsync(Guid tenantId, IncomingMessage message, CancellationToken ct = default)
    {
        // A partir de acá, todo lo que lea el DbContext (vía los repositorios)
        // queda automáticamente filtrado a este tenant por el global query filter.
        _currentTenant.SetTenant(tenantId);

        var tenant = await _tenants.GetByIdAsync(tenantId, ct);

        var conversation = await _conversations.GetOrCreateAsync(message.CustomerPhoneNumber, ct);

        var handler = _resolver.Resolve(conversation.State);
        var result = await handler.HandleAsync(tenant, conversation, message, ct);

        conversation.State = result.NextState;
        conversation.LastMessageAt = DateTime.UtcNow;
        await _conversations.SaveAsync(conversation, ct);
    }
}