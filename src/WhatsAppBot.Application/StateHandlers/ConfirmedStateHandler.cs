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
        await _sender.SendTextAsync(
            tenant.WhatsAppPhoneNumberId, conversation.CustomerPhoneNumber,
            "Tu pedido ya está confirmado 🙌 Si necesitás algo más, escribinos.", ct);

        return new StateResult(ConversationState.Confirmed);
    }
}
