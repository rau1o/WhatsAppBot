using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatsAppBot.Infrastructure.Identity
{
    // AppUser vive acá y no en Domain a propósito: hereda de IdentityUser<Guid>,
    // que es un tipo de Microsoft.AspNetCore.Identity. Meterlo en Domain
    // rompería la regla de "Domain sin dependencias a paquetes externos".
    // El concepto de negocio "quién puede loguearse y a qué tenant pertenece"
    // vive acá como detalle técnico de autenticación, no como entidad de dominio.
    public class AppUser : IdentityUser<Guid>
    {
        public Guid TenantId { get; set; }
        public string DisplayName { get; set; } = default!;
    }
}
