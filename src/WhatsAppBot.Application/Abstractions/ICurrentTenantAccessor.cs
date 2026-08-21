using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatsAppBot.Application.Abstractions
{
    // Contexto ambient del tenant "actual" dentro de un scope de DI
    // (un request HTTP, o la ejecución de un job de Hangfire).
    // Se setea una sola vez al principio del caso de uso y de ahí en más
    // los repositorios lo leen para filtrar — así ningún repositorio nuevo
    // puede "olvidarse" de filtrar por tenant.
    public interface ICurrentTenantAccessor
    {
        Guid? TenantId { get; }

        void SetTenant(Guid tenantId);
    }
}
