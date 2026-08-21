using System.ComponentModel.DataAnnotations;

namespace WhatsAppBot.Api.Contracts
{
    public record LoginRequest(string Email, string Password);

    public record LoginResponse(string Token, DateTime ExpiresAtUtc, Guid TenantId, string Role);

    public record ChangePasswordRequest(
        [Required] string CurrentPassword,
        [Required, MinLength(8)] string NewPassword
    );
}
