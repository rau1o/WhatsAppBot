using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Application.Messaging;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Application.StateHandlers;

// Placeholder de fase 1. En fase 2 se reemplaza por el menú de catálogo real.
public class GreetedStateHandler : IStateHandler
{
    private readonly IWhatsAppMessageSender _sender;

    public GreetedStateHandler(IWhatsAppMessageSender sender)
    {
        _sender = sender;
    }

    public ConversationState State => ConversationState.Greeted;

    public async Task<StateResult> HandleAsync(
        Tenant tenant,
        Conversation conversation,
        IncomingMessage message,
        CancellationToken ct)
    {
        await _sender.SendTextAsync(
            tenant.WhatsAppPhoneNumberId,
            conversation.CustomerPhoneNumber,
            "En breve un asesor te va a atender. Mientras tanto, ¡gracias por tu paciencia! 🙌",
            ct);

        return new StateResult(ConversationState.Greeted);
    }
}
