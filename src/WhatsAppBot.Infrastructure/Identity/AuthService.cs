using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Infrastructure.Persistence;

namespace WhatsAppBot.Infrastructure.Identity
{
    public class AuthService : IAuthService
    {
        // 30 días — el usuario que abre el panel de vez en cuando no tiene
        // que volver a loguearse cada hora; el que lo usa activamente,
        // gracias a la rotación en cada refresh, nunca ve este vencimiento
        // mientras siga usándolo.
        private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

        private readonly UserManager<AppUser> _userManager;
        private readonly JwtTokenService _tokenService;
        private readonly IEmailSender _emailSender;
        private readonly AdminPanelOptions _adminPanelOptions;
        private readonly WhatsAppBotDbContext _db;
        public AuthService(
            UserManager<AppUser> userManager,
            JwtTokenService tokenService,
            IEmailSender emailSender,
            IOptions<AdminPanelOptions> adminPanelOptions,
            WhatsAppBotDbContext db)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _emailSender = emailSender;
            _adminPanelOptions = adminPanelOptions.Value;
            _db = db;
        }

        public async Task<(bool Success, string? Error)> ChangePasswordAsync(
        Guid userId, string currentPassword, string newPassword, CancellationToken ct)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null) return (false, "Usuario no encontrado.");

            // ChangePasswordAsync valida la contraseña actual Y la política de la
            // nueva (largo mínimo, mayúscula, dígito, símbolo) en un solo paso —
            // no hace falta chequear nada a mano de este lado.
            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

            if (!result.Succeeded)
                return (false, string.Join(" ", result.Errors.Select(e => e.Description)));

            return (true, null);
        }

        public async Task<AuthResult?> LoginAsync(string email, string password, CancellationToken ct)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user is null) return null;

            // Un usuario desactivado (DeactivateUserAsync) queda con lockout
            // seteado — no puede volver a loguearse aunque la contraseña sea correcta.
            if (await _userManager.IsLockedOutAsync(user)) return null;

            // CheckPasswordAsync ya hace el hashing/comparación — nunca comparamos
            // contraseñas a mano ni las guardamos en texto plano.
            var passwordValid = await _userManager.CheckPasswordAsync(user, password);
            if (!passwordValid) return null;

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "Staff";

            var (token, expiresAt) = _tokenService.GenerateToken(user, role);
            var refreshToken = await IssueRefreshTokenAsync(user.Id, ct);

            return new AuthResult(token, expiresAt, user.TenantId, role, refreshToken);
        }

        public async Task RequestPasswordResetAsync(string email, CancellationToken ct)
        {
            var user = await _userManager.FindByEmailAsync(email);

            // Silencioso a propósito si no existe — el caller (AuthController)
            // siempre le muestra al usuario el mismo mensaje genérico "si el
            // email existe, te llegó un correo", para no filtrar qué emails
            // están registrados en el sistema.
            if (user is null) return;

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // El token trae caracteres que rompen una URL tal cual (+, /, =)
            // — WebEncoders.Base64UrlEncode lo deja seguro para viajar como
            // query string.
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var resetLink = $"{_adminPanelOptions.PublicBaseUrl}/reset-password?email={Uri.EscapeDataString(email)}&token={encodedToken}";

            var html = $"""
                <p>Hola,</p>
                <p>Recibimos un pedido para restablecer tu contraseña del panel de WhatsApp Bot.</p>
                <p><a href="{resetLink}">Hacé click acá para elegir una contraseña nueva</a></p>
                <p>Si vos no pediste esto, podés ignorar este correo — tu contraseña actual sigue funcionando igual.</p>
                <p>Este link vence en 24 horas.</p>
                """;

            await _emailSender.SendAsync(email, "Restablecer tu contraseña", html, ct);
        }

        public async Task<(bool Success, string? Error)> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct)
        {
            var user = await _userManager.FindByEmailAsync(email);
            // Mensaje genérico también acá — no distinguir "email no existe"
            // de "token inválido/vencido".
            if (user is null) return (false, "El link no es válido o ya venció. Pedí uno nuevo.");

            string decodedToken;
            try
            {
                decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            }
            catch (FormatException)
            {
                return (false, "El link no es válido o ya venció. Pedí uno nuevo.");
            }

            var result = await _userManager.ResetPasswordAsync(user, decodedToken, newPassword);
            if (!result.Succeeded)
            {
                // Acá sí devolvemos el detalle real (política de contraseña,
                // etc.) — a esta altura el usuario ya demostró ser dueño del
                // email al hacer click en el link, no hay riesgo de filtrar nada.
                var isTokenError = result.Errors.Any(e => e.Code is "InvalidToken");
                var message = isTokenError
                    ? "El link no es válido o ya venció. Pedí uno nuevo."
                    : string.Join(" ", result.Errors.Select(e => e.Description));
                return (false, message);
            }

            return (true, null);
        }
        public async Task<AuthResult?> RefreshAsync(string refreshToken, CancellationToken ct)
        {
            var hash = HashToken(refreshToken);
            var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

            if (stored is null) return null; // nunca existió — token inválido, sin más

            if (!stored.IsActive)
            {
                // Este hash existe pero ya no está activo. Si específicamente
                // fue REVOCADO porque ya se usó antes (tiene ReplacedByTokenHash),
                // alguien está reintentando un refresh token viejo — señal de
                // que pudo filtrarse. Respuesta: revocar TODOS los tokens
                // activos de este usuario, forzando un re-login real en
                // cualquier sesión (legítima o no).
                if (stored.ReplacedByTokenHash is not null)
                {
                    var allActive = await _db.RefreshTokens
                        .Where(t => t.UserId == stored.UserId && t.RevokedAtUtc == null)
                        .ToListAsync(ct);

                    foreach (var t in allActive) t.RevokedAtUtc = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);
                }

                return null;
            }

            var user = await _userManager.FindByIdAsync(stored.UserId.ToString());
            if (user is null) return null;

            if (await _userManager.IsLockedOutAsync(user)) return null;

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "Staff";

            var (jwt, expiresAt) = _tokenService.GenerateToken(user, role);

            // Rotación: el token usado queda revocado y apunta al nuevo —
            // así, si alguien lo reintenta después, lo detectamos arriba.
            var newRefreshToken = await IssueRefreshTokenAsync(user.Id, ct);
            stored.RevokedAtUtc = DateTime.UtcNow;
            stored.ReplacedByTokenHash = HashToken(newRefreshToken);
            await _db.SaveChangesAsync(ct);

            return new AuthResult(jwt, expiresAt, user.TenantId, role, newRefreshToken);
        }

        public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct)
        {
            var hash = HashToken(refreshToken);
            var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

            if (stored is null || !stored.IsActive) return; // ya inválido, nada que hacer

            stored.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        private async Task<string> IssueRefreshTokenAsync(Guid userId, CancellationToken ct)
        {
            // 256 bits de aleatoriedad criptográfica — no es un JWT, es solo
            // un identificador opaco que el cliente guarda y devuelve tal cual.
            var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

            _db.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = HashToken(rawToken),
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.Add(RefreshTokenLifetime)
            });
            await _db.SaveChangesAsync(ct);

            return rawToken;
        }

        private static string HashToken(string rawToken)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    }
}
