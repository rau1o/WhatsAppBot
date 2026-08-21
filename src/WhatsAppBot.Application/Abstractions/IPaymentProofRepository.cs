using WhatsAppBot.Domain.Entities;

namespace WhatsAppBot.Application.Abstractions;

public interface IPaymentProofRepository
{
    Task AddAsync(PaymentProof proof, CancellationToken ct);
    Task<PaymentProof?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PaymentProof>> ListPendingAsync(CancellationToken ct);
    Task UpdateAsync(PaymentProof proof, CancellationToken ct);
}
