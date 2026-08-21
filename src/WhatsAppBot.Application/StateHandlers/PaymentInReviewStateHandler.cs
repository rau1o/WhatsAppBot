using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Application.Messaging;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Application.StateHandlers;

// No avanza el estado por sí solo — el panel admin es quien mueve la
// conversación a Confirmed (o de vuelta a AwaitingPayment si se rechaza)
// cuando un empleado valida el comprobante.
public class PaymentInReviewStateHandler : IStateHandler
{
    private readonly IWhatsAppMessageSender _sender;

    public PaymentInReviewStateHandler(IWhatsAppMessageSender sender)
    {
        _sender = sender;
    }

    public ConversationState State => ConversationState.PaymentInReview;

    public async Task<StateResult> HandleAsync(
        Tenant tenant, Conversation conversation, IncomingMessage message, CancellationToken ct)
    {
        await _sender.SendTextAsync(
            tenant.WhatsAppPhoneNumberId, conversation.CustomerPhoneNumber,
            "Tu comprobante está en revisión. Te avisamos apenas lo confirmemos 🙏", ct);

        return new StateResult(ConversationState.PaymentInReview);
    }
}
