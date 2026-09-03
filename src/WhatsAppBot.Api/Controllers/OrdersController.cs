using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppBot.Api.Contracts;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderRepository _orders;
    private readonly IConversationRepository _conversations;
    private readonly IPaymentProofRepository _paymentProofs;
    public OrdersController(IOrderRepository orders, IConversationRepository conversations, IPaymentProofRepository paymentProofs)

    {
        _orders = orders;
        _conversations = conversations;
        _paymentProofs = paymentProofs;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string status, CancellationToken ct)
    {
        if (!Enum.TryParse<OrderFulfillmentStatus>(status, ignoreCase: true, out var fulfillmentStatus))
            return BadRequest(new { message = $"Estado inválido: '{status}'. Válidos: Pending, ReadyForPickup, Completed." });

        var orders = await _orders.ListByFulfillmentStatusAsync(fulfillmentStatus, ct);

        var result = new List<FulfillmentOrderDto>();
        foreach (var order in orders)
        {
            var conversation = await _conversations.GetByIdAsync(order.ConversationId, ct);

            result.Add(new FulfillmentOrderDto(
                order.Id,
                conversation?.CustomerPhoneNumber ?? "(desconocido)",
                order.Total,
                order.FulfillmentStatus!.Value.ToString(),
                order.CreatedAt,
                order.Items.Select(i => new OrderItemLineDto(i.ProductName, i.Quantity, i.UnitPrice)).ToList()));
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/mark-ready")]
    public Task<IActionResult> MarkReady(Guid id, CancellationToken ct)
        => ChangeFulfillmentStatusAsync(id, expectedCurrent: OrderFulfillmentStatus.Pending, newStatus: OrderFulfillmentStatus.ReadyForPickup, ct);

    [HttpPost("{id:guid}/mark-completed")]
    public Task<IActionResult> MarkCompleted(Guid id, CancellationToken ct)
        => ChangeFulfillmentStatusAsync(id, expectedCurrent: OrderFulfillmentStatus.ReadyForPickup, newStatus: OrderFulfillmentStatus.Completed, ct);
    // Deshace una aprobación de comprobante hecha por error. Solo se
    // permite mientras el pedido sigue en "Pending" — si el staff ya lo
    // marcó listo o entregado, deshacerla no tiene sentido operativo (ya
    // se preparó o entregó), así que se bloquea con un mensaje explícito
    // en vez de dejar el sistema en un estado incoherente.
    [HttpPost("{id:guid}/undo-approval")]
    public async Task<IActionResult> UndoApproval(Guid id, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order is null) return NotFound();

        if (order.FulfillmentStatus != OrderFulfillmentStatus.Pending)
            return Conflict(new { message = $"Este pedido ya está en estado '{order.FulfillmentStatus}' — no se puede deshacer la aprobación desde acá." });

        var proof = await _paymentProofs.GetLatestApprovedForOrderAsync(id, ct);
        if (proof is null)
            return Conflict(new { message = "No encontramos el comprobante aprobado de este pedido." });

        proof.Status = PaymentProofStatus.Pending;
        proof.ReviewedByUserId = null;
        proof.ReviewedAt = null;
        await _paymentProofs.UpdateAsync(proof, ct);

        order.FulfillmentStatus = null;
        var orderSaved = await _orders.SaveAsync(order, ct);
        if (!orderSaved) return Conflict(new { message = "No pudimos actualizar el pedido — probá de nuevo." });

        // Solo tocamos la conversación si SIGUE en Confirmed — si el cliente
        // ya escribió de nuevo y arrancó otro pedido (ver ConfirmedStateHandler),
        // forzarla de vuelta a AwaitingPayment le rompería lo que esté
        // haciendo ahora. En ese caso, el comprobante queda pendiente de
        // revisión igual, pero sin tocar por dónde anda la conversación.
        var conversation = await _conversations.GetByIdAsync(order.ConversationId, ct);
        if (conversation is not null && conversation.State == ConversationState.Confirmed)
        {
            conversation.State = ConversationState.AwaitingPayment;
            await _conversations.SaveAsync(conversation, ct);
        }

        // A propósito NO le mandamos ningún mensaje al cliente acá — fue un
        // error del staff, no algo que el cliente necesite saber sin
        // contexto. Si hace falta avisarle algo, que el staff lo haga
        // directo por WhatsApp con el contexto real de lo que pasó.
        return NoContent();
    }

    private async Task<IActionResult> ChangeFulfillmentStatusAsync(
        Guid orderId, OrderFulfillmentStatus expectedCurrent, OrderFulfillmentStatus newStatus, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(orderId, ct);
        if (order is null) return NotFound();

        // Chequeo explícito del estado esperado — evita, por ejemplo, que
        // dos empleados marcando "listo" y "entregado" casi al mismo tiempo
        // dejen el pedido en un estado inconsistente.
        if (order.FulfillmentStatus != expectedCurrent)
            return Conflict(new { message = $"Este pedido está en estado '{order.FulfillmentStatus}', no se puede aplicar este cambio." });

        order.FulfillmentStatus = newStatus;
        var saved = await _orders.SaveAsync(order, ct);

        if (!saved) return Conflict(new { message = "No pudimos actualizar el pedido — probá de nuevo." });

        return NoContent();
    }
}
