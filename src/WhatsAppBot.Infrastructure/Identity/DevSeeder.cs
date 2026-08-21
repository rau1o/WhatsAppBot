using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Domain.Entities;

namespace WhatsAppBot.Infrastructure.Identity
{
    // Solo para desarrollo local — crea un tenant y un usuario admin de prueba
    // si la base está vacía. En producción los tenants y usuarios se crean
    // desde un flujo de onboarding real, no acá.
    public static class DevSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Persistence.WhatsAppBotDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

            if (!await roleManager.RoleExistsAsync("Owner"))
                await roleManager.CreateAsync(new IdentityRole<Guid>("Owner"));

            if (!await roleManager.RoleExistsAsync(TenantRoles.Staff))
                await roleManager.CreateAsync(new IdentityRole<Guid>(TenantRoles.Staff));

            var tenant = await db.Tenants.FirstOrDefaultAsync();
            if (tenant is null)
            {
                tenant = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Name = "Tienda de prueba",
                    WhatsAppPhoneNumberId = "000000000000000", // reemplazar por el real
                    LocationLatitude = -17.7833,
                    LocationLongitude = -63.1821,
                    LocationName = "Tienda de prueba",
                    LocationAddress = "Av. Ejemplo 123, Santa Cruz",
                    FacadePhotoUrl = "https://example.com/fachada.jpg",
                    PaymentQrImageUrl = "https://example.com/qr-transferencia.jpg" // reemplazar por el QR real del tenant
                };
                db.Tenants.Add(tenant);
                await db.SaveChangesAsync();
            }

            if (!await db.Products.IgnoreQueryFilters().AnyAsync(p => p.TenantId == tenant.Id))
            {
                db.Products.AddRange(
                    new Product { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Router TP-Link Archer C6", Price = 350, IsActive = true },
                    new Product { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Switch 8 puertos Gigabit", Price = 280, IsActive = true },
                    new Product { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Cable de red Cat6 (rollo 300m)", Price = 620, IsActive = true }
                );
                await db.SaveChangesAsync();
            }

            const string adminEmail = "admin@tienda.test";
            if (await userManager.FindByEmailAsync(adminEmail) is null)
            {
                var user = new AppUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    TenantId = tenant.Id,
                    DisplayName = "Admin de prueba",
                    EmailConfirmed = true
                };

                // Contraseña de desarrollo únicamente — nunca hardcodear así en producción.
                await userManager.CreateAsync(user, "Admin123!");
                await userManager.AddToRoleAsync(user, TenantRoles.Owner);
            }
        }
    }
}
