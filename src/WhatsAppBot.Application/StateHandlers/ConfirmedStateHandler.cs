using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Application.Messaging;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Application.StateHandlers;

public class ConfirmedStateHandler : IStateHandler
{
    private readonly IWhatsAppMessageSender _sender;

    public ConfirmedStateHandler(IWhatsAppMessageSender sender)
    {
        _sender = sender;
    }

    public ConversationState State => ConversationState.Confirmed;

    public async Task<StateResult> HandleAsync(
        Tenant tenant, Conversation conversation, IncomingMessage message, CancellationToken ct)
    {
        // Confirmed es terminal para EL PEDIDO, pero no para la conversación
        // — si el cliente escribe de nuevo (sea el mismo día o semanas
        // después), lo más útil es asumir que quiere hacer un pedido nuevo,
        // no repetirle para siempre "tu pedido ya está confirmado". El
        // pedido anterior queda intacto (Submitted + su FulfillmentStatus);
        // el próximo GetOrCreateDraftAsync arma uno nuevo desde cero, ya que
        // el viejo no tiene Status = Draft.
        await _sender.SendTextAsync(
            tenant.WhatsAppPhoneNumberId, conversation.CustomerPhoneNumber,
            "¡Genial! Empecemos un pedido nuevo 🙌", ct);

        return new StateResult(ConversationState.BrowsingCatalog, ContinueImmediately: true);
    }
}
