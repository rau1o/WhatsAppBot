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

            await CreateTenantIfRequestedAsync(db, userManager);
        }

        // "Alta de tenant" de emergencia: hoy no hay UI para esto (pendiente
        // conocido del proyecto). Igual que el resto de los mecanismos acá,
        // solo actúa si se setean estas 4 variables de entorno EXPLÍCITAMENTE
        // — y como SeedAsync solo se llama dentro del bloque
        // `if (app.Environment.IsDevelopment())` de Program.cs, nunca corre
        // en Railway (que corre como Production), así que es seguro dejarlo
        // en el código de forma permanente.
        private static async Task CreateTenantIfRequestedAsync(Persistence.WhatsAppBotDbContext db, UserManager<AppUser> userManager)
        {
            var name = Environment.GetEnvironmentVariable("NEW_TENANT_NAME");
            var phoneNumberId = Environment.GetEnvironmentVariable("NEW_TENANT_PHONE_NUMBER_ID");
            var ownerEmail = Environment.GetEnvironmentVariable("NEW_TENANT_OWNER_EMAIL");
            var ownerPassword = Environment.GetEnvironmentVariable("NEW_TENANT_OWNER_PASSWORD");

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(phoneNumberId)
                || string.IsNullOrWhiteSpace(ownerEmail) || string.IsNullOrWhiteSpace(ownerPassword))
                return;

            var existingTenant = await db.Tenants.IgnoreQueryFilters()
               .FirstOrDefaultAsync(t => t.WhatsAppPhoneNumberId == phoneNumberId);

            Tenant newTenant;
            if (existingTenant is not null)
            {
                // El tenant ya existe (probablemente porque la vez anterior
                // el usuario falló, ej. por la política de contraseñas) —
                // no lo recreamos, pero seguimos igual para intentar crear
                // el usuario que falta.
                Console.WriteLine($"[NEW TENANT] Ya existe un tenant con WhatsAppPhoneNumberId = {phoneNumberId} — reintentando solo la creación del usuario.");
                newTenant = existingTenant;
            }
            else
            {
                newTenant = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    WhatsAppPhoneNumberId = phoneNumberId,
                    // Datos placeholder — el dueño los completa desde el panel
                    // (Configuración) apenas entra por primera vez.
                    LocationLatitude = 0,
                    LocationLongitude = 0,
                    LocationName = name,
                    LocationAddress = "Pendiente de configurar desde el panel",
                    FacadePhotoUrl = "",
                    PaymentQrImageUrl = null
                };
                db.Tenants.Add(newTenant);
                await db.SaveChangesAsync();
            }

            if (await userManager.FindByEmailAsync(ownerEmail) is not null)
            {
                Console.WriteLine($"[NEW TENANT] Ya existe un usuario con el email {ownerEmail} — no se tocó nada más.");
                return;
            }

            var owner = new AppUser
            {
                UserName = ownerEmail,
                Email = ownerEmail,
                TenantId = newTenant.Id,
                DisplayName = "Owner",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(owner, ownerPassword);
            if (!result.Succeeded)
            {
                Console.WriteLine($"[NEW TENANT] El tenant '{name}' se creó (Id {newTenant.Id}), pero el usuario falló: " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }

            await userManager.AddToRoleAsync(owner, TenantRoles.Owner);

            Console.WriteLine($"[NEW TENANT] Tenant '{name}' creado (Id {newTenant.Id}). Owner: {ownerEmail}.");

        }
    }
}
