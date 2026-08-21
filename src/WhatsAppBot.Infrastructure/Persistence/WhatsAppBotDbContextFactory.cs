using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WhatsAppBot.Application.Abstractions;

namespace WhatsAppBot.Infrastructure.Persistence;

// Usada solo por la CLI de EF Core (dotnet ef migrations add / update),
// nunca en runtime. Sin esto, "dotnet ef" no sabe cómo instanciar el
// DbContext porque vive en un proyecto de clases, no en el Api.
public class WhatsAppBotDbContextFactory : IDesignTimeDbContextFactory<WhatsAppBotDbContext>
{
    public WhatsAppBotDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("WHATSAPPBOT_CONNECTION")
            ?? "Host=localhost;Database=whatsappbot;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<WhatsAppBotDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        // El global query filter no se evalúa al generar/aplicar migraciones,
        // así que un accessor sin tenant seteado es suficiente acá.
        return new WhatsAppBotDbContext(options, new DesignTimeTenantAccessor());
    }

    private class DesignTimeTenantAccessor : ICurrentTenantAccessor
    {
        public Guid? TenantId => null;
        public void SetTenant(Guid tenantId) { }
    }
}