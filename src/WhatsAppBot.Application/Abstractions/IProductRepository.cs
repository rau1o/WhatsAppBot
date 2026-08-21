using WhatsAppBot.Domain.Entities;

namespace WhatsAppBot.Application.Abstractions;

public interface IProductRepository
{
    // Para el bot: solo lo que el cliente puede ver.
    Task<IReadOnlyList<Product>> ListActiveAsync(CancellationToken ct);
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct);

    // Para el panel admin: ve todo, incluidos los inactivos.
    Task<IReadOnlyList<Product>> ListAllAsync(CancellationToken ct);
    Task AddAsync(Product product, CancellationToken ct);
    Task UpdateAsync(Product product, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
