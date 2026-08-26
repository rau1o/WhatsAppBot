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

    public OrdersController(IOrderRepository orders, IConversationRepository conversations)
    {
        _orders = orders;
        _conversations = conversations;
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
