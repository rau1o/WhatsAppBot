using System.Text;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Application.Messaging;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Application.StateHandlers;

// Fase 2: arma el resumen del pedido y lo marca como enviado.
// La confirmación de pago (fase 3) arranca desde acá — este handler
// termina donde fase 3 va a empezar a construir.
public class OrderReviewStateHandler : IStateHandler
{
    private readonly IWhatsAppMessageSender _sender;
    private readonly IOrderRepository _orders;

    public OrderReviewStateHandler(IWhatsAppMessageSender sender, IOrderRepository orders)
    {
        _sender = sender;
        _orders = orders;
    }

    public ConversationState State => ConversationState.BuildingOrder;

    public async Task<StateResult> HandleAsync(
        Tenant tenant,
        Conversation conversation,
        IncomingMessage message,
        CancellationToken ct)
    {
        var to = conversation.CustomerPhoneNumber;
        var phoneNumberId = tenant.WhatsAppPhoneNumberId;

        var order = await _orders.GetOrCreateDraftAsync(conversation.Id, ct);

        if (order.Items.Count == 0)
        {
            await _sender.SendTextAsync(phoneNumberId, to,
                "Todavía no agregaste ningún producto. Te muestro el catálogo de nuevo:", ct);
            return new StateResult(ConversationState.BrowsingCatalog);
        }

        order.Status = OrderStatus.Submitted;
        var saved =await _orders.SaveAsync(order, ct);

        if (!saved)
        {
            // Mismo criterio que en CatalogStateHandler: nunca avanzar el
            // estado (ni pedir el comprobante) si el pedido no se guardó de verdad.
            await _sender.SendTextAsync(phoneNumberId, to,
                "Uy, tuvimos un problema confirmando tu pedido. ¿Podés tocar \"Finalizar pedido\" de nuevo?", ct);
            return new StateResult(ConversationState.BuildingOrder);
        }

        await _sender.SendTextAsync(phoneNumberId, to, OrderSummaryFormatter.BuildSummary(order), ct);

        if (!string.IsNullOrWhiteSpace(tenant.PaymentQrImageUrl))
        {
            await _sender.SendImageByUrlAsync(phoneNumberId, to, tenant.PaymentQrImageUrl,
                "Escaneá este QR para hacer tu transferencia 👆", ct);
        }

        // Si el tenant todavía no cargó su QR, seguimos igual — no tiene
        // sentido trabar el pedido del cliente por una configuración
        // pendiente del lado de la tienda. El panel admin debería avisarle
        // al dueño que le falta cargarlo (fuera de alcance por ahora).

        await _sender.SendTextAsync(phoneNumberId, to,
            "Una vez que hagas el pago, mandanos una foto o captura de tu comprobante 📸", ct);

        return new StateResult(ConversationState.AwaitingPayment);
    }
   

}
