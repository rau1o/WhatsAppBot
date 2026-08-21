using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Infrastructure.Identity;

namespace WhatsAppBot.Infrastructure.Persistence
{
    // IdentityDbContext<AppUser, IdentityRole<Guid>, Guid> agrega las tablas
    // AspNetUsers/AspNetRoles/etc. — son tablas de infraestructura de auth,
    // separadas conceptualmente de Tenants/Conversations aunque compartan la
    // misma base de datos física por simplicidad operativa.
    public class WhatsAppBotDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
    {
        private readonly ICurrentTenantAccessor _currentTenant;

        public WhatsAppBotDbContext(DbContextOptions<WhatsAppBotDbContext> options, ICurrentTenantAccessor currentTenant)
            : base(options)
        {
            _currentTenant = currentTenant;
        }

        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<Conversation> Conversations => Set<Conversation>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<PaymentProof> PaymentProofs => Set<PaymentProof>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // registra el modelo de Identity primero

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(WhatsAppBotDbContext).Assembly);

            // Filtro global por tenant: cualquier query sobre estas entidades
            // queda automáticamente acotada al tenant actual. Si nadie seteó
            // el tenant todavía, TenantId es null y el filtro no matchea nada
            // — falla cerrado, no abierto.
            // Nota: Tenant NO se filtra — es la raíz multi-tenant. AppUser
            // tampoco: se filtra por consulta explícita (ej. login busca por
            // email en todos los usuarios). OrderItem no tiene TenantId propio
            // — queda protegido porque siempre se llega a él vía Order.Items.
            modelBuilder.Entity<Conversation>()
                .HasQueryFilter(c => c.TenantId == _currentTenant.TenantId);

            modelBuilder.Entity<Product>()
                .HasQueryFilter(p => p.TenantId == _currentTenant.TenantId);

            modelBuilder.Entity<Order>()
                .HasQueryFilter(o => o.TenantId == _currentTenant.TenantId);

            modelBuilder.Entity<PaymentProof>()
                .HasQueryFilter(p => p.TenantId == _currentTenant.TenantId);
        }
    }
}
