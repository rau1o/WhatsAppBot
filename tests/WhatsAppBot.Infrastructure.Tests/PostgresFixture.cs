using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Infrastructure.MultiTenancy;
using WhatsAppBot.Infrastructure.Persistence;
using Xunit;

namespace WhatsAppBot.Infrastructure.Tests;

// IAsyncLifetime: xUnit lo respeta como setup/teardown asíncrono. El
// contenedor se levanta UNA vez para toda la colección (ver
// PostgresCollection abajo) — levantarlo por test sería correcto pero
// insoportablemente lento (Testcontainers tarda unos segundos en arrancar).
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("whatsappbot_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Mismas migraciones reales que corren contra Supabase en
        // producción — es justamente el punto de este proyecto de tests:
        // probar el esquema y el comportamiento real, no una aproximación.
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    // Cada test arma su propio DbContext (con su propio ICurrentTenantAccessor)
    // apuntando a este mismo Postgres — simula exactamente lo que pasa en
    // producción, donde cada job de Hangfire tiene su propio scope/DbContext.
    public WhatsAppBotDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WhatsAppBotDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new WhatsAppBotDbContext(options, new CurrentTenantAccessor());
    }

    public WhatsAppBotDbContext CreateContext(ICurrentTenantAccessor currentTenant)
    {
        var options = new DbContextOptionsBuilder<WhatsAppBotDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new WhatsAppBotDbContext(options, currentTenant);
    }
}

[CollectionDefinition("Postgres")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    // Vacía a propósito — xUnit solo necesita esta clase para asociar el
    // fixture con el nombre "Postgres" que usan los [Collection("Postgres")].
}
