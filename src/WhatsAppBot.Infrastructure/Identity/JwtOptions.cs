using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatsAppBot.Infrastructure.Identity
{
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string Issuer { get; set; } = "WhatsAppBot";
        public string Audience { get; set; } = "WhatsAppBot.Admin";
        public string SigningKey { get; set; } = default!; // mínimo 32 caracteres, va en appsettings/secret manager, nunca hardcodeado
        public int ExpiryMinutes { get; set; } = 60;
    }
}
