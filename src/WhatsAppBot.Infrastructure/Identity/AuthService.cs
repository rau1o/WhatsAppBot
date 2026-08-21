using Microsoft.AspNetCore.Identity;
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

        public AuthService(UserManager<AppUser> userManager, JwtTokenService tokenService)
        {
            _userManager = userManager;
            _tokenService = tokenService;
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
    }
}
