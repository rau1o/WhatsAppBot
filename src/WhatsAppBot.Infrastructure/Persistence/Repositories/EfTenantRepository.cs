using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Domain.Entities;

namespace WhatsAppBot.Infrastructure.Persistence.Repositories
{
    public class EfTenantRepository : ITenantRepository
    {
        private readonly WhatsAppBotDbContext _db;

        public EfTenantRepository(WhatsAppBotDbContext db)
        {
            _db = db;
        }

        public async Task<Tenant> GetByIdAsync(Guid id, CancellationToken ct)
            => await _db.Tenants.FirstAsync(t => t.Id == id, ct);

        public async Task<Tenant?> GetByWhatsAppPhoneNumberIdAsync(string phoneNumberId, CancellationToken ct)
            => await _db.Tenants.FirstOrDefaultAsync(t => t.WhatsAppPhoneNumberId == phoneNumberId, ct);

        public async Task UpdateAsync(Tenant tenant, CancellationToken ct)
        {
            _db.Tenants.Update(tenant);
            await _db.SaveChangesAsync(ct);
        }
    }
}
