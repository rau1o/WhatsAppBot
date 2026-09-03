using WhatsAppBot.Domain.Entities;

namespace WhatsAppBot.Application.Abstractions;

public interface IPaymentProofRepository
{
    Task AddAsync(PaymentProof proof, CancellationToken ct);
    Task<PaymentProof?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PaymentProof>> ListPendingAsync(CancellationToken ct);

    // Para poder deshacer una aprobación por error — se busca por OrderId
    // (no por PaymentProofId) porque el punto de entrada natural es la
    // pantalla de "Pedidos", que trabaja con Order, no con PaymentProof.
    Task<PaymentProof?> GetLatestApprovedForOrderAsync(Guid orderId, CancellationToken ct);

    Task UpdateAsync(PaymentProof proof, CancellationToken ct);
}
