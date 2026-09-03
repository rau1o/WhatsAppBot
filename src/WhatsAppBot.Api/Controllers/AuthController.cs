using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.IdentityModel.Tokens.Jwt;
using WhatsAppBot.Api.Contracts;
using WhatsAppBot.Application.Abstractions;

namespace WhatsAppBot.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth)
        {
            _auth = auth;
        }

        [HttpPost("login")]
        [EnableRateLimiting("login")] // frena fuerza bruta de contraseñas — ver política en Program.cs
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
        {
            var result = await _auth.LoginAsync(request.Email, request.Password, ct);

            // Mensaje genérico a propósito: no distinguir "email no existe" de
            // "contraseña incorrecta" evita filtrar qué emails están registrados.
            if (result is null) return Unauthorized(new { message = "Credenciales inválidas" });

            return Ok(new LoginResponse(result.Token, result.ExpiresAtUtc, result.TenantId, result.Role));
        }

        // Cualquier usuario logueado puede cambiar SU PROPIA contraseña — no
        // requiere ningún rol particular, a diferencia de la gestión de otros
        // usuarios (esa vive en UsersController, restringida a Owner).
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
        {
            var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? throw new InvalidOperationException("El JWT no tiene claim 'sub'.");
            var userId = Guid.Parse(sub);

            var (success, error) = await _auth.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ct);

            if (!success) return BadRequest(new { message = error });

            return NoContent();
        }
        // Siempre devuelve el mismo mensaje genérico, exista o no el email —
        // evita que alguien use este endpoint para averiguar qué emails
        // están registrados en el sistema.
        [HttpPost("forgot-password")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
        {
            await _auth.RequestPasswordResetAsync(request.Email, ct);
            return Ok(new { message = "Si el email existe en el sistema, te llegó un correo con el link para restablecer tu contraseña." });
        }

        [HttpPost("reset-password")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
        {
            var (success, error) = await _auth.ResetPasswordAsync(request.Email, request.Token, request.NewPassword, ct);

            if (!success) return BadRequest(new { message = error });

            return NoContent();
        }
    }
}
