namespace WhatsAppBot.Application.Abstractions;

public record TenantUserSummary(Guid Id, string Email, string DisplayName, string Role, bool IsActive);

public record InviteUserResult(TenantUserSummary User, string TemporaryPassword);

// Puerto: Application solo sabe que puede listar/invitar/desactivar
// usuarios del tenant actual. No sabe que eso implica Identity — mismo
// patrón que IAuthService.
public interface IUserManagementService
{
    Task<IReadOnlyList<TenantUserSummary>> ListUsersAsync(CancellationToken ct);

    // El rol es "Owner" o "Staff" — se valida en la implementación.
    Task<InviteUserResult> InviteUserAsync(string email, string displayName, string role, CancellationToken ct);

    Task ChangeRoleAsync(Guid userId, string newRole, CancellationToken ct);
    Task DeactivateUserAsync(Guid userId, CancellationToken ct);
    Task ReactivateUserAsync(Guid userId, CancellationToken ct);
}
