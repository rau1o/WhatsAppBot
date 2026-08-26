using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using WhatsAppBot.Api.Contracts;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Api.Controllers;

[ApiController]
[Route("api/payment-proofs")]
[Authorize]
public class PaymentProofsController : ControllerBase
{
    private readonly IPaymentProofRepository _paymentProofs;
    private readonly IOrderRepository _orders;
    private readonly IConversationRepository _conversations;
    private readonly ITenantRepository _tenants;
    private readonly IWhatsAppMessageSender _sender;
    private readonly ICurrentTenantAccessor _currentTenant;

    public PaymentProofsController(
        IPaymentProofRepository paymentProofs,
        IOrderRepository orders,
        IConversationRepository conversations,
        ITenantRepository tenants,
        IWhatsAppMessageSender sender,
        ICurrentTenantAccessor currentTenant)
    {
        _paymentProofs = paymentProofs;
        _orders = orders;
        _conversations = conversations;
        _tenants = tenants;
        _sender = sender;
        _currentTenant = currentTenant;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> ListPending(CancellationToken ct)
    {
        var proofs = await _paymentProofs.ListPendingAsync(ct);

        var result = new List<PaymentProofDto>();
        foreach (var proof in proofs)
        {
            var order = await _orders.GetByIdAsync(proof.OrderId, ct);
            var conversation = order is null ? null : await _conversations.GetByIdAsync(order.ConversationId, ct);

            // Si por algún motivo el pedido o la conversación ya no existen,
            // igual mostramos el comprobante (con placeholders) en vez de
            // que un dato inconsistente tumbe toda la lista de pendientes.
            result.Add(new PaymentProofDto(
                proof.Id, proof.OrderId,
                conversation?.CustomerPhoneNumber ?? "(desconocido)",
                order?.Total ?? 0,
                proof.Status.ToString(), proof.CreatedAt));
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
        => await ReviewAsync(id, approve: true, ct);

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
        => await ReviewAsync(id, approve: false, ct);

    private async Task<IActionResult> ReviewAsync(Guid proofId, bool approve, CancellationToken ct)
    {
        var proof = await _paymentProofs.GetByIdAsync(proofId, ct);
        if (proof is null) return NotFound();

        if (proof.Status != PaymentProofStatus.Pending)
            return Conflict(new { message = $"Este comprobante ya fue {proof.Status}." });

        var order = await _orders.GetByIdAsync(proof.OrderId, ct)
            ?? throw new InvalidOperationException($"Pedido {proof.OrderId} no encontrado para el comprobante {proofId}.");

        var conversation = await _conversations.GetByIdAsync(order.ConversationId, ct)
            ?? throw new InvalidOperationException($"Conversación {order.ConversationId} no encontrada.");

        var tenant = await _tenants.GetByIdAsync(_currentTenant.TenantId!.Value, ct);

        proof.Status = approve ? PaymentProofStatus.Approved : PaymentProofStatus.Rejected;
        proof.ReviewedByUserId = GetCurrentUserId();
        proof.ReviewedAt = DateTime.UtcNow;
        await _paymentProofs.UpdateAsync(proof, ct);

        if (approve)
        {
            conversation.State = ConversationState.Confirmed;
            order.FulfillmentStatus = OrderFulfillmentStatus.Pending; // arranca el seguimiento de preparación
            await _orders.SaveAsync(order, ct);

            await _sender.SendTextAsync(tenant.WhatsAppPhoneNumberId, conversation.CustomerPhoneNumber,
                "¡Confirmamos tu pago! 🎉 Tu pedido ya está en preparación. Gracias por tu compra.", ct);

        }
        else
        {
            conversation.State = ConversationState.AwaitingPayment;
            await _sender.SendTextAsync(tenant.WhatsAppPhoneNumberId, conversation.CustomerPhoneNumber,
                "No pudimos validar tu comprobante 😕 ¿Nos podés mandar una foto más clara del pago?", ct);
        }

        await _conversations.SaveAsync(conversation, ct);

        return Ok(new PaymentProofDto(
            proof.Id, proof.OrderId, conversation.CustomerPhoneNumber, order.Total,
            proof.Status.ToString(), proof.CreatedAt));
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? throw new InvalidOperationException("El JWT no tiene claim 'sub'.");
        return Guid.Parse(sub);
    }
}
