using System.Collections.Concurrent;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Infrastructure.Persistence;
//  Ninguna de las clases de este archivo se registra en DependencyInjection.cs
// (reemplazadas por las implementaciones Ef* sobre Postgres). Se dejan
// disponibles para usar en tests unitarios de Application sin necesitar
// una base de datos real.

public class InMemoryProductRepository : IProductRepository
{
    private readonly ConcurrentDictionary<Guid, Product> _products = new();
    private readonly ICurrentTenantAccessor _currentTenant;

    public InMemoryProductRepository(ICurrentTenantAccessor currentTenant)
    {
        _currentTenant = currentTenant;
    }

    public void Seed(Product product) => _products[product.Id] = product;

    public Task<IReadOnlyList<Product>> ListActiveAsync(CancellationToken ct)
    {
        IReadOnlyList<Product> result = _products.Values.Where(p => p.IsActive).ToList();
        return Task.FromResult(result);
    }

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult(_products.GetValueOrDefault(id));

    public Task<IReadOnlyList<Product>> ListAllAsync(CancellationToken ct)
    {
        var tenantId = RequireTenantId();
        IReadOnlyList<Product> result = _products.Values.Where(p => p.TenantId == tenantId).ToList();
        return Task.FromResult(result);
    }

    public Task AddAsync(Product product, CancellationToken ct)
    {
        product.Id = product.Id == Guid.Empty ? Guid.NewGuid() : product.Id;
        product.TenantId = RequireTenantId();
        _products[product.Id] = product;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Product product, CancellationToken ct)
    {
        _products[product.Id] = product;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct)
    {
        _products.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    private Guid RequireTenantId()
        => _currentTenant.TenantId
           ?? throw new InvalidOperationException("No hay un tenant actual seteado en este scope.");
}

public class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<Guid, Order> _draftsByConversation = new();

    public Task<Order> GetOrCreateDraftAsync(Guid conversationId, CancellationToken ct)
    {
        var order = _draftsByConversation.GetOrAdd(conversationId, _ => new Order
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Status = OrderStatus.Draft,
            CreatedAt = DateTime.UtcNow
        });

        return Task.FromResult(order);
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult(_draftsByConversation.Values.FirstOrDefault(o => o.Id == id));

    public Task<Order?> GetLatestForConversationAsync(Guid conversationId, CancellationToken ct)
        => Task.FromResult(_draftsByConversation.GetValueOrDefault(conversationId));

    public Task SaveAsync(Order order, CancellationToken ct)
    {
        _draftsByConversation[order.ConversationId] = order;
        return Task.CompletedTask;
    }
}

public class InMemoryPaymentProofRepository : IPaymentProofRepository
{
    private readonly ConcurrentDictionary<Guid, PaymentProof> _proofs = new();
    private readonly ICurrentTenantAccessor _currentTenant;

    public InMemoryPaymentProofRepository(ICurrentTenantAccessor currentTenant)
    {
        _currentTenant = currentTenant;
    }

    public Task AddAsync(PaymentProof proof, CancellationToken ct)
    {
        _proofs[proof.Id] = proof;
        return Task.CompletedTask;
    }

    public Task<PaymentProof?> GetByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult(_proofs.GetValueOrDefault(id));

    public Task<IReadOnlyList<PaymentProof>> ListPendingAsync(CancellationToken ct)
    {
        var tenantId = RequireTenantId();
        IReadOnlyList<PaymentProof> result = _proofs.Values
            .Where(p => p.TenantId == tenantId && p.Status == PaymentProofStatus.Pending)
            .ToList();
        return Task.FromResult(result);
    }

    public Task UpdateAsync(PaymentProof proof, CancellationToken ct)
    {
        _proofs[proof.Id] = proof;
        return Task.CompletedTask;
    }

    private Guid RequireTenantId()
        => _currentTenant.TenantId
           ?? throw new InvalidOperationException("No hay un tenant actual seteado en este scope.");
}

public class InMemoryTenantRepository : ITenantRepository
{
    private readonly ConcurrentDictionary<Guid, Tenant> _tenants = new();
    private readonly ConcurrentDictionary<string, Guid> _byPhoneNumberId = new();

    public void Seed(Tenant tenant)
    {
        _tenants[tenant.Id] = tenant;
        _byPhoneNumberId[tenant.WhatsAppPhoneNumberId] = tenant.Id;
    }

    public Task<Tenant> GetByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult(_tenants[id]);

    public Task<Tenant?> GetByWhatsAppPhoneNumberIdAsync(string phoneNumberId, CancellationToken ct)
    {
        if (_byPhoneNumberId.TryGetValue(phoneNumberId, out var id))
            return Task.FromResult<Tenant?>(_tenants[id]);

        return Task.FromResult<Tenant?>(null);
    }

    public Task UpdateAsync(Tenant tenant, CancellationToken ct)
    {
        _tenants[tenant.Id] = tenant;
        return Task.CompletedTask;
    }
}

public class InMemoryConversationRepository : IConversationRepository
{
    private readonly ConcurrentDictionary<(Guid TenantId, string Phone), Conversation> _conversations = new();
    private readonly ICurrentTenantAccessor _currentTenant;

    public InMemoryConversationRepository(ICurrentTenantAccessor currentTenant)
    {
        _currentTenant = currentTenant;
    }

    public Task<Conversation> GetOrCreateAsync(string customerPhoneNumber, CancellationToken ct)
    {
        var tenantId = RequireTenantId();
        var key = (tenantId, customerPhoneNumber);

        var conversation = _conversations.GetOrAdd(key, _ => new Conversation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerPhoneNumber = customerPhoneNumber,
            State = ConversationState.New
        });

        return Task.FromResult(conversation);
    }

    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult(_conversations.Values.FirstOrDefault(c => c.Id == id));

    public Task SaveAsync(Conversation conversation, CancellationToken ct)
    {
        _conversations[(conversation.TenantId, conversation.CustomerPhoneNumber)] = conversation;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Conversation>> ListRecentAsync(CancellationToken ct)
    {
        var tenantId = RequireTenantId();
        IReadOnlyList<Conversation> result = _conversations.Values
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.LastMessageAt)
            .ToList();

        return Task.FromResult(result);
    }

    private Guid RequireTenantId()
        => _currentTenant.TenantId
           ?? throw new InvalidOperationException("No hay un tenant actual seteado en este scope.");
}
