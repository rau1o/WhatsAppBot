using System.ComponentModel.DataAnnotations;

namespace WhatsAppBot.Api.Contracts
{
    public record LoginRequest(string Email, string Password);

    public record LoginResponse(string Token, DateTime ExpiresAtUtc, Guid TenantId, string Role, string RefreshToken);

    public record RefreshRequest([Required] string RefreshToken);
    public record ChangePasswordRequest(
        [Required] string CurrentPassword,
        [Required, MinLength(8)] string NewPassword
    );

    public record ForgotPasswordRequest([Required, EmailAddress] string Email);

    public record ResetPasswordRequest(
        [Required, EmailAddress] string Email,
        [Required] string Token,
        [Required, MinLength(8)] string NewPassword
    );
}
