using FluentAssertions;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Infrastructure.MultiTenancy;
using WhatsAppBot.Infrastructure.Persistence.Repositories;
using Xunit;

namespace WhatsAppBot.Infrastructure.Tests;

[Collection("Postgres")]
public class MultiTenancyIsolationTests
{
    private readonly PostgresFixture _fixture;

    public MultiTenancyIsolationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // El global query filter (WhatsAppBotDbContext, keyed en
    // ICurrentTenantAccessor.TenantId) es la única barrera real entre los
    // datos de dos negocios distintos — si esto se rompe, un tenant podría
    // ver los productos, pedidos o conversaciones de otro. Vale la pena
    // demostrarlo contra Postgres real, no asumirlo.
    [Fact]
    public async Task Un_tenant_nunca_ve_productos_de_otro_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var seedDb = _fixture.CreateContext())
        {
            seedDb.Products.Add(new Product { Id = Guid.NewGuid(), TenantId = tenantA, Name = "Producto de A", Price = 10, IsActive = true });
            seedDb.Products.Add(new Product { Id = Guid.NewGuid(), TenantId = tenantB, Name = "Producto de B", Price = 20, IsActive = true });
            await seedDb.SaveChangesAsync();
        }

        var accessorA = new CurrentTenantAccessor();
        accessorA.SetTenant(tenantA);
        await using var dbA = _fixture.CreateContext(accessorA);
        var repoA = new EfProductRepository(dbA, accessorA);

        var resultA = await repoA.ListAllAsync(page: 1, pageSize: 50, CancellationToken.None);

        resultA.Items.Should().ContainSingle();
        resultA.Items.Single().Name.Should().Be("Producto de A");
        resultA.Items.Should().NotContain(p => p.Name == "Producto de B");
    }

    // Fail-closed: sin tenant seteado, tiene que devolver vacío (o fallar
    // fuerte, según el repo), nunca "todo" — un bug acá filtraría datos de
    // TODOS los tenants a la vez.
    [Fact]
    public async Task Sin_tenant_seteado_ListActiveAsync_no_devuelve_datos_de_otros_tenants()
    {
        var tenantA = Guid.NewGuid();

        await using (var seedDb = _fixture.CreateContext())
        {
            seedDb.Products.Add(new Product { Id = Guid.NewGuid(), TenantId = tenantA, Name = "Producto de A", Price = 10, IsActive = true });
            await seedDb.SaveChangesAsync();
        }

        // Accessor SIN SetTenant() — TenantId queda null.
        var accessorSinTenant = new CurrentTenantAccessor();
        await using var db = _fixture.CreateContext(accessorSinTenant);
        var repo = new EfProductRepository(db, accessorSinTenant);

        var result = await repo.ListActiveAsync(CancellationToken.None);

        result.Should().BeEmpty("fail-closed: sin tenant seteado no se debe ver nada, nunca todo");
    }
}
