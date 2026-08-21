using System.ComponentModel.DataAnnotations;

namespace WhatsAppBot.Api.Contracts;

public record UserDto(Guid Id, string Email, string DisplayName, string Role, bool IsActive);

public record InviteUserRequest(
    [Required, EmailAddress] string Email,
    [Required, MaxLength(200)] string DisplayName,
    [Required] string Role
);

public record InviteUserResponse(UserDto User, string TemporaryPassword);

public record ChangeRoleRequest([Required] string Role);
