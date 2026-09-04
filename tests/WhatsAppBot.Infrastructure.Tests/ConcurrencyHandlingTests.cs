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
public class ConcurrencyHandlingTests
{
    private readonly PostgresFixture _fixture;

    public ConcurrencyHandlingTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // Reproduce el escenario real: un OrderItem que estaba trackeado en
    // memoria desaparece de la base por fuera de este DbContext (acá lo
    // forzamos con un DELETE directo — en producción pasó por una limpieza
    // manual de datos de prueba mientras un job viejo seguía en danza).
    // Confirma DOS cosas: que SaveAsync devuelve false en vez de tirar la
    // excepción hacia arriba, y que el MISMO DbContext sigue sirviendo para
    // operaciones NO relacionadas después — que es justo lo que
    // ChangeTracker.Clear() existe para garantizar.
    [Fact]
    public async Task Un_conflicto_de_concurrencia_no_corrompe_el_DbContext_para_operaciones_posteriores()
    {
        var tenantId = Guid.NewGuid();
        var accessor = new CurrentTenantAccessor();
        accessor.SetTenant(tenantId);

        Guid conversationId1, conversationId2, productAId;

        await using (var seedDb = _fixture.CreateContext())
        {
            var tenant = new Tenant
            {
                Id = tenantId, Name = "Tienda", WhatsAppPhoneNumberId = Guid.NewGuid().ToString(),
                LocationLatitude = 0, LocationLongitude = 0, LocationName = "x", LocationAddress = "x", FacadePhotoUrl = "x"
            };
            var productA = new Product { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Producto A", Price = 10, IsActive = true };
            var productC = new Product { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Producto C", Price = 30, IsActive = true };
            productAId = productA.Id;

            var conv1 = new Conversation { Id = Guid.NewGuid(), TenantId = tenantId, CustomerPhoneNumber = "59170000001", State = ConversationState.BrowsingCatalog, LastMessageAt = DateTime.UtcNow };
            var conv2 = new Conversation { Id = Guid.NewGuid(), TenantId = tenantId, CustomerPhoneNumber = "59170000002", State = ConversationState.BrowsingCatalog, LastMessageAt = DateTime.UtcNow };
            conversationId1 = conv1.Id;
            conversationId2 = conv2.Id;

            seedDb.Tenants.Add(tenant);
            seedDb.Products.AddRange(productA, productC);
            seedDb.Conversations.AddRange(conv1, conv2);
            await seedDb.SaveChangesAsync();
        }

        await using var db = _fixture.CreateContext(accessor);
        var repo = new EfOrderRepository(db, accessor, NullLogger<EfOrderRepository>.Instance);

        // 1. Creamos el pedido de la conversación 1 con Producto A.
        var order1 = await repo.GetOrCreateDraftAsync(conversationId1, CancellationToken.None);
        var product = await db.Products.FirstAsync(p => p.Id == productAId);
        order1.AddOrIncrementItem(product, 1);
        (await repo.SaveAsync(order1, CancellationToken.None)).Should().BeTrue();

        // 2. Por fuera de este DbContext, borramos ese OrderItem directo de
        // la base — simula el caso real (limpieza manual de datos mientras
        // un job viejo seguía en danza).
        await using (var externalDb = _fixture.CreateContext())
        {
            await externalDb.Database.ExecuteSqlRawAsync(
                "DELETE FROM order_items WHERE \"OrderId\" = {0}", order1.Id);
        }

        // 3. En el MISMO DbContext (order1.Items todavía tiene el item en
        // memoria), intentamos incrementarlo — genera un UPDATE contra una
        // fila que ya no existe.
        order1.AddOrIncrementItem(product, 1);
        var savedAfterExternalDelete = await repo.SaveAsync(order1, CancellationToken.None);

        savedAfterExternalDelete.Should().BeFalse("la fila fue borrada por fuera de este DbContext");

        // 4. Lo importante: el MISMO repo/DbContext tiene que seguir
        // sirviendo para algo completamente distinto después — si
        // ChangeTracker.Clear() no estuviera, esto fallaría con un error
        // que no tiene nada que ver.
        var order2 = await repo.GetOrCreateDraftAsync(conversationId2, CancellationToken.None);
        var productC = await db.Products.FirstAsync(p => p.Name == "Producto C");
        order2.AddOrIncrementItem(productC, 1);

        var savedUnrelatedOrder = await repo.SaveAsync(order2, CancellationToken.None);
        savedUnrelatedOrder.Should().BeTrue("el DbContext tiene que seguir siendo utilizable después del conflicto anterior");
    }
}
