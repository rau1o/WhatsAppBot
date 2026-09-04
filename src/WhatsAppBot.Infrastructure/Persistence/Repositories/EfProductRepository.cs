using Microsoft.EntityFrameworkCore;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Domain.Entities;

namespace WhatsAppBot.Infrastructure.Persistence.Repositories;

public class EfProductRepository : IProductRepository
{
    private readonly WhatsAppBotDbContext _db;
    private readonly ICurrentTenantAccessor _currentTenant;

    public EfProductRepository(WhatsAppBotDbContext db, ICurrentTenantAccessor currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<IReadOnlyList<Product>> ListActiveAsync(CancellationToken ct)
    {
        RequireTenantId();

        return await _db.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<PagedResult<Product>> ListAllAsync(int page, int pageSize, CancellationToken ct)
    {
        RequireTenantId();

        var query = _db.Products.OrderBy(p => p.Name);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Product>(items, page, pageSize, totalCount);
    }
       

    public async Task AddAsync(Product product, CancellationToken ct)
    {
        var tenantId = RequireTenantId();

        // El TenantId nunca lo decide el caller — siempre el tenant actual
        // del scope, así es imposible crear (o "regalar") un producto en
        // el tenant equivocado por un bug en el controller.
        product.Id = product.Id == Guid.Empty ? Guid.NewGuid() : product.Id;
        product.TenantId = tenantId;

        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Product product, CancellationToken ct)
    {
        var tenantId = RequireTenantId();

        var existing = await _db.Products.FirstOrDefaultAsync(p => p.Id == product.Id, ct)
            ?? throw new InvalidOperationException($"Producto {product.Id} no encontrado.");

        if (existing.TenantId != tenantId)
            throw new InvalidOperationException(
                $"El producto {product.Id} pertenece a otro tenant.");

        existing.Name = product.Name;
        existing.Description = product.Description;
        existing.Price = product.Price;
        existing.ImageUrl = product.ImageUrl;
        existing.IsActive = product.IsActive;

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var tenantId = RequireTenantId();

        var existing = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (existing is null) return; // ya no existe — delete es idempotente

        if (existing.TenantId != tenantId)
            throw new InvalidOperationException($"El producto {id} pertenece a otro tenant.");

        _db.Products.Remove(existing);
        await _db.SaveChangesAsync(ct);
    }

    private Guid RequireTenantId()
        => _currentTenant.TenantId
           ?? throw new InvalidOperationException("No hay un tenant actual seteado en este scope.");
}
