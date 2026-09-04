using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;
using WhatsAppBot.Infrastructure.MultiTenancy;
using WhatsAppBot.Infrastructure.Persistence.Repositories;
using Xunit;

namespace WhatsAppBot.Infrastructure.Tests;

[Collection("Postgres")]
public class EfOrderRepositoryTests
{
    private readonly PostgresFixture _fixture;

    public EfOrderRepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // Este es EL bug real que más nos costó cazar en producción: al agregar
    // un segundo producto DISTINTO a un pedido ya existente, EF clasificaba
    // el OrderItem nuevo como "Modified" en vez de "Added" — porque su Guid
    // lo generamos nosotros (Order.AddOrIncrementItem), no la base, y
    // llamar _db.Entry(x) en CUALQUIER entidad dispara DetectChanges() de
    // TODO el contexto, clasificando mal el item antes de que el código
    // llegara a corregirlo. El resultado real: un UPDATE contra una fila
    // que nunca existió, "0 rows affected".
    //
    // El repo en memoria (InMemoryOrderRepository) NUNCA pudo reproducir
    // esto — es puro comportamiento de EF Core + Postgres. Por eso este
    // test vive acá y no en WhatsAppBot.Application.Tests.
    [Fact]
    public async Task Agregar_un_segundo_producto_distinto_lo_inserta_correctamente()
    {
        var tenantId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var (tenant, productA, productB) = await SeedTenantWithTwoProductsAsync(tenantId);
        await SeedConversationAsync(tenantId, conversationId);

        // Job 1: agrega el primer producto (simula el primer mensaje del cliente).
        await using (var db1 = _fixture.CreateContext(TenantAccessor(tenantId)))
        {
            var repo1 = new EfOrderRepository(db1, TenantAccessor(tenantId), NullLogger<EfOrderRepository>.Instance);
            var order1 = await repo1.GetOrCreateDraftAsync(conversationId, CancellationToken.None);
            order1.AddOrIncrementItem(productA, 1);
            (await repo1.SaveAsync(order1, CancellationToken.None)).Should().BeTrue();
        }

        // Job 2: DbContext completamente nuevo — igual que un segundo job de
        // Hangfire para el segundo mensaje del cliente. Acá es donde el bug real ocurría.
        await using (var db2 = _fixture.CreateContext(TenantAccessor(tenantId)))
        {
            var repo2 = new EfOrderRepository(db2, TenantAccessor(tenantId), NullLogger<EfOrderRepository>.Instance);
            var order2 = await repo2.GetOrCreateDraftAsync(conversationId, CancellationToken.None);
            order2.AddOrIncrementItem(productB, 2);

            var saved = await repo2.SaveAsync(order2, CancellationToken.None);
            saved.Should().BeTrue("agregar un producto distinto a un pedido existente tiene que insertar, no fallar");
        }

        // Verificación final con un tercer DbContext, totalmente aparte.
        await using var verifyDb = _fixture.CreateContext(TenantAccessor(tenantId));
        var finalOrder = await verifyDb.Orders
            .Include(o => o.Items)
            .FirstAsync(o => o.ConversationId == conversationId);

        finalOrder.Items.Should().HaveCount(2);
        finalOrder.Items.Should().Contain(i => i.ProductId == productA.Id && i.Quantity == 1);
        finalOrder.Items.Should().Contain(i => i.ProductId == productB.Id && i.Quantity == 2);
    }

    [Fact]
    public async Task Elegir_el_mismo_producto_dos_veces_incrementa_en_vez_de_duplicar()
    {
        var tenantId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var (tenant, productA, _) = await SeedTenantWithTwoProductsAsync(tenantId);
        await SeedConversationAsync(tenantId, conversationId);

        await using (var db1 = _fixture.CreateContext(TenantAccessor(tenantId)))
        {
            var repo1 = new EfOrderRepository(db1, TenantAccessor(tenantId), NullLogger<EfOrderRepository>.Instance);
            var order1 = await repo1.GetOrCreateDraftAsync(conversationId, CancellationToken.None);
            order1.AddOrIncrementItem(productA, 2);
            await repo1.SaveAsync(order1, CancellationToken.None);
        }

        await using (var db2 = _fixture.CreateContext(TenantAccessor(tenantId)))
        {
            var repo2 = new EfOrderRepository(db2, TenantAccessor(tenantId), NullLogger<EfOrderRepository>.Instance);
            var order2 = await repo2.GetOrCreateDraftAsync(conversationId, CancellationToken.None);
            order2.AddOrIncrementItem(productA, 3);
            await repo2.SaveAsync(order2, CancellationToken.None);
        }

        await using var verifyDb = _fixture.CreateContext(TenantAccessor(tenantId));
        var finalOrder = await verifyDb.Orders
            .Include(o => o.Items)
            .FirstAsync(o => o.ConversationId == conversationId);

        finalOrder.Items.Should().ContainSingle(); // una sola fila...
        finalOrder.Items.Single().Quantity.Should().Be(5); // ...con 2 + 3
    }

    private async Task<(Tenant Tenant, Product ProductA, Product ProductB)> SeedTenantWithTwoProductsAsync(Guid tenantId)
    {
        await using var db = _fixture.CreateContext(); // sin tenant seteado — Add() no pasa por el query filter

        var tenant = new Tenant
        {
            Id = tenantId, Name = "Tienda de prueba", WhatsAppPhoneNumberId = Guid.NewGuid().ToString(),
            LocationLatitude = 0, LocationLongitude = 0, LocationName = "x", LocationAddress = "x", FacadePhotoUrl = "x"
        };
        var productA = new Product { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Producto A", Price = 10, IsActive = true };
        var productB = new Product { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Producto B", Price = 20, IsActive = true };

        db.Tenants.Add(tenant);
        db.Products.AddRange(productA, productB);
        await db.SaveChangesAsync();

        return (tenant, productA, productB);
    }

    private async Task SeedConversationAsync(Guid tenantId, Guid conversationId)
    {
        await using var db = _fixture.CreateContext();
        db.Conversations.Add(new Conversation
        {
            Id = conversationId, TenantId = tenantId, CustomerPhoneNumber = "59170000000",
            State = ConversationState.BrowsingCatalog, LastMessageAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static CurrentTenantAccessor TenantAccessor(Guid tenantId)
    {
        var accessor = new CurrentTenantAccessor();
        accessor.SetTenant(tenantId);
        return accessor;
    }
}
