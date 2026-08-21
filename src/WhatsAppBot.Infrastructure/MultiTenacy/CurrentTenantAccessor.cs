using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatsAppBot.Application.Abstractions;

namespace WhatsAppBot.Infrastructure.MultiTenacy
{
    // Registrado como Scoped en DI. Con Hangfire.AspNetCore, cada job corre
    // dentro de su propio scope — así como cada request HTTP tiene el suyo —
    // por lo que esta instancia nunca se comparte entre tenants distintos.
    public class CurrentTenantAccessor : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; private set; }

        public void SetTenant(Guid tenantId)
        {
            if (TenantId.HasValue && TenantId.Value != tenantId)
                throw new InvalidOperationException(
                    $"El tenant actual ya estaba fijado en {TenantId}. No se puede cambiar a {tenantId} dentro del mismo scope.");

            TenantId = tenantId;
        }
    }
}
