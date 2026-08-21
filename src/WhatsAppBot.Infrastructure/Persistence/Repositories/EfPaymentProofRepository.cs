using Microsoft.EntityFrameworkCore;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Infrastructure.Persistence.Repositories;

public class EfPaymentProofRepository : IPaymentProofRepository
{
    private readonly WhatsAppBotDbContext _db;
    private readonly ICurrentTenantAccessor _currentTenant;

    public EfPaymentProofRepository(WhatsAppBotDbContext db, ICurrentTenantAccessor currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task AddAsync(PaymentProof proof, CancellationToken ct)
    {
        _db.PaymentProofs.Add(proof);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PaymentProof?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _db.PaymentProofs.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<PaymentProof>> ListPendingAsync(CancellationToken ct)
    {
        RequireTenantId();

        return await _db.PaymentProofs
            .Where(p => p.Status == PaymentProofStatus.Pending)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(PaymentProof proof, CancellationToken ct)
    {
        _db.PaymentProofs.Update(proof);
        await _db.SaveChangesAsync(ct);
    }

    private Guid RequireTenantId()
        => _currentTenant.TenantId
           ?? throw new InvalidOperationException("No hay un tenant actual seteado en este scope.");
}
