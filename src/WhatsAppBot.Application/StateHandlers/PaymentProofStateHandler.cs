using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Application.Messaging;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Application.StateHandlers;

// Fase 3: espera a que el cliente mande la foto del comprobante.
// A propósito NO descargamos ni guardamos la imagen — solo registramos
// que llegó (el media_id de WhatsApp) y su estado. El staff la revisa
// directamente en la app de WhatsApp Business del negocio, donde el
// comprobante ya está. Esto evita cualquier costo de storage que crezca
// con cada cliente.
public class PaymentProofStateHandler : IStateHandler
{
    private readonly IWhatsAppMessageSender _sender;
    private readonly IOrderRepository _orders;
    private readonly IPaymentProofRepository _paymentProofs;

    public PaymentProofStateHandler(
        IWhatsAppMessageSender sender,       
        IOrderRepository orders,
        IPaymentProofRepository paymentProofs)
    {
        _sender = sender;        
        _orders = orders;
        _paymentProofs = paymentProofs;
    }

    public ConversationState State => ConversationState.AwaitingPayment;

    public async Task<StateResult> HandleAsync(
        Tenant tenant,
        Conversation conversation,
        IncomingMessage message,
        CancellationToken ct)
    {
        var to = conversation.CustomerPhoneNumber;
        var phoneNumberId = tenant.WhatsAppPhoneNumberId;

        if (message.MediaId is null)
        {
            await _sender.SendTextAsync(phoneNumberId, to,
                "Todavía estamos esperando la foto de tu comprobante de pago 🙏", ct);
            return new StateResult(ConversationState.AwaitingPayment);
        }

        var order = await _orders.GetLatestForConversationAsync(conversation.Id, ct);
        if (order is null)
        {
            // No debería pasar en un flujo normal (implica que llegó acá sin
            // haber pasado por OrderReviewStateHandler) — lo tratamos como
            // error de estado en vez de asumir silenciosamente.
            await _sender.SendTextAsync(phoneNumberId, to,
                "Hubo un problema encontrando tu pedido. Un asesor te va a contactar en breve.", ct);
            return new StateResult(ConversationState.AwaitingPayment);
        }
       
        var proof = new PaymentProof
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            OrderId = order.Id,
            WhatsAppMediaId = message.MediaId,
            Status = PaymentProofStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _paymentProofs.AddAsync(proof, ct);

        await _sender.SendTextAsync(phoneNumberId, to,
            "¡Recibimos tu comprobante! En breve lo revisamos y te confirmamos ✅", ct);

        return new StateResult(ConversationState.PaymentInReview);
    }
}
