using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Application.Messaging;
using WhatsAppBot.Application.StateHandlers;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Application;

// Se ejecuta en background (Hangfire) después de que el webhook encola
// el mensaje. Carga la conversación, delega en el handler del estado
// actual, y persiste el nuevo estado.
public class MessageProcessor : IMessageProcessor
{
    // Estados donde el cliente dejó un pedido a medio hacer — si pasa
    // demasiado tiempo sin que responda, lo consideramos abandonado.
    // BrowsingCatalog queda afuera a propósito: mirar el catálogo sin
    // comprometerse a nada no tiene "abandono" real que resetear.
    private static readonly ConversationState[] StuckStates =
    {
        ConversationState.BuildingOrder,
        ConversationState.AwaitingPayment,
        ConversationState.PaymentInReview
    };

    // Funciona en CUALQUIER estado — no depende de en qué parte del flujo
    // esté el cliente. Comparación exacta (no "Contains") para no disparar
    // por accidente con un producto que tenga "cancelar" en el nombre.
    private static readonly HashSet<string> ResetKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "reiniciar", "cancelar", "cancelar pedido", "reset", "empezar de nuevo", "empezar de cero"
    };


    private readonly StateHandlerResolver _resolver;
    private readonly ITenantRepository _tenants;
    private readonly IConversationRepository _conversations;
    private readonly IOrderRepository _orders;
    private readonly IWhatsAppMessageSender _sender;
    private readonly ICurrentTenantAccessor _currentTenant;
    private readonly ICorrelationIdAccessor _correlationId;
    private readonly ConversationTimeoutOptions _timeoutOptions;
    private readonly ILogger<MessageProcessor> _logger;

    public MessageProcessor(
        StateHandlerResolver resolver,
        ITenantRepository tenants,
        IConversationRepository conversations,
        IOrderRepository orders,
        IWhatsAppMessageSender sender,
        ICurrentTenantAccessor currentTenant,
        ICorrelationIdAccessor correlationId,
        IOptions<ConversationTimeoutOptions> timeoutOptions,
        ILogger<MessageProcessor> logger)

    {
        _resolver = resolver;
        _tenants = tenants;
        _conversations = conversations;
        _orders = orders;
        _sender = sender;
        _currentTenant = currentTenant;
        _correlationId = correlationId;
        _timeoutOptions = timeoutOptions.Value;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid tenantId, IncomingMessage message, string correlationId, CancellationToken ct = default)
    {
        _correlationId.SetCorrelationId(correlationId);

        // Todo log dentro de este scope (acá y en cualquier repositorio/
        // handler que use el mismo ILogger) queda taggeado con el mismo
        // CorrelationId — así se puede buscar en Railway todos los eventos
        // de un mensaje puntual, incluso si el webhook y el job corrieron
        // en momentos distintos.
        using var _ = _logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });

        // A partir de acá, todo lo que lea el DbContext (vía los repositorios)
        // queda automáticamente filtrado a este tenant por el global query filter.
        _currentTenant.SetTenant(tenantId);

        var tenant = await _tenants.GetByIdAsync(tenantId, ct);

        var conversation = await _conversations.GetOrCreateAsync(message.CustomerPhoneNumber, ct);

        var wasResetByCommand = await TryHandleResetCommandAsync(tenant, conversation, message, ct);
        if (!wasResetByCommand)
        {
            await ResetIfStaleAsync(tenant, conversation, ct);
        }

        // La mayoría de las veces este loop corre UNA sola vuelta. Solo sigue de
        // largo cuando un handler pide explícitamente continuar al estado nuevo
        // sin esperar otro mensaje real (ContinueImmediately = true) — por
        // ejemplo, mostrar el catálogo apenas termina el saludo, o el resumen +
        // QR apenas el cliente toca "Finalizar pedido". El límite de 5 es una
        // red de seguridad contra un bug futuro que encadene estados sin fin.
        var currentMessage = message;
        const int maxChainedTransitions = 5;

        for (var i = 0; i < maxChainedTransitions; i++)
        {
            var handler = _resolver.Resolve(conversation.State);
            var result = await handler.HandleAsync(tenant, conversation, currentMessage, ct);

            conversation.State = result.NextState;
            conversation.LastMessageAt = DateTime.UtcNow;
            await _conversations.SaveAsync(conversation, ct);

            if (!result.ContinueImmediately) break;

            // Simulamos un mensaje "vacío" (sin texto, sin botón, sin nada) para
            // que el próximo handler dispare su comportamiento de "recién entré
            // a este estado" — el mismo que ya usa cuando el cliente manda un
            // mensaje que no matchea ningún botón conocido.
            currentMessage = new IncomingMessage(message.CustomerPhoneNumber, message.TenantPhoneNumberId, null, null, null, null);
        }

    }

    private async Task<bool> TryHandleResetCommandAsync(Tenant tenant, Conversation conversation, IncomingMessage message, CancellationToken ct)
    {
        if (message.Text is null || !ResetKeywords.Contains(message.Text.Trim())) return false;

        await AbandonOrderAndResetAsync(tenant, conversation,
            "Reiniciamos tu pedido. ¡Empecemos de nuevo! 🙌", ct);

        return true;
    }

    private async Task ResetIfStaleAsync(Tenant tenant, Conversation conversation, CancellationToken ct)
    {
        if (!StuckStates.Contains(conversation.State)) return;

        var staleThreshold = TimeSpan.FromHours(_timeoutOptions.StaleAfterHours);
        if (DateTime.UtcNow - conversation.LastMessageAt <= staleThreshold) return;

        await AbandonOrderAndResetAsync(tenant, conversation,
            "Tu pedido anterior quedó pendiente por mucho tiempo, así que lo dejamos sin efecto. ¡Empecemos de nuevo! 🙌", ct);
    }

    // Compartido entre el comando manual y el reset por timeout: marca el
    // pedido activo (si hay uno) como abandonado, avisa por WhatsApp, y
    // deja la conversación lista para arrancar de cero en BrowsingCatalog.
    private async Task AbandonOrderAndResetAsync(Tenant tenant, Conversation conversation, string notificationMessage, CancellationToken ct)
    {
        var order = await _orders.GetLatestForConversationAsync(conversation.Id, ct);
        if (order is not null && order.Status is OrderStatus.Draft or OrderStatus.Submitted)
        {
            order.Status = OrderStatus.Abandoned;
            await _orders.SaveAsync(order, ct);
        }

        await _sender.SendTextAsync(tenant.WhatsAppPhoneNumberId, conversation.CustomerPhoneNumber, notificationMessage, ct);

        conversation.State = ConversationState.BrowsingCatalog;
    }

}