using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatsAppBot.Application.Abstractions
{
    public record AuthResult(string Token, DateTime ExpiresAtUtc, Guid TenantId, string Role, string RefreshToken);

    // Application solo sabe que puede "intentar loguear con email/password".
    // No sabe si eso implica Identity, JWT, hashing, etc. — eso es Infrastructure.
    public interface IAuthService
    {
        Task<AuthResult?> LoginAsync(string email, string password, CancellationToken ct);

        // Requiere la contraseña actual — no es un "admin resetea la contraseña
        // de otro", eso ya existe en IUserManagementService. Esto es "el usuario
        // logueado cambia la suya propia", siempre validando la actual primero.
        Task<(bool Success, string? Error)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct);

        // Siempre "exitoso" desde afuera (nunca revela si el email existe o
        // no) — si el usuario existe, dispara el email con el link de reseteo.
        Task RequestPasswordResetAsync(string email, CancellationToken ct);

        Task<(bool Success, string? Error)> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct);

        // Cambia el JWT vencido (o por vencer) por uno nuevo, sin pedir
        // contraseña de nuevo — rota el refresh token en cada uso (el viejo
        // queda inválido) para poder detectar si alguno se filtró.
        Task<AuthResult?> RefreshAsync(string refreshToken, CancellationToken ct);

        // Para logout explícito — invalida el refresh token del lado del
        // servidor, no alcanza con solo borrarlo del cliente.
        Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct);
    }
}
