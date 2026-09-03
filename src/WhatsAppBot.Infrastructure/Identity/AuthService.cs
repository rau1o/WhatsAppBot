using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatsAppBot.Application.Abstractions;

namespace WhatsAppBot.Infrastructure.Identity
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly JwtTokenService _tokenService;
        private readonly IEmailSender _emailSender;
        private readonly AdminPanelOptions _adminPanelOptions;

        public AuthService(
            UserManager<AppUser> userManager,
            JwtTokenService tokenService,
            IEmailSender emailSender,
            IOptions<AdminPanelOptions> adminPanelOptions)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _emailSender = emailSender;
            _adminPanelOptions = adminPanelOptions.Value;
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

            return new AuthResult(token, expiresAt, user.TenantId, role);
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
    }
}
