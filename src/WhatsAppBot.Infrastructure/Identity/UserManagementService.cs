using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using WhatsAppBot.Application.Abstractions;

namespace WhatsAppBot.Infrastructure.Identity;

public class UserManagementService : IUserManagementService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ICurrentTenantAccessor _currentTenant;

    public UserManagementService(UserManager<AppUser> userManager, ICurrentTenantAccessor currentTenant)
    {
        _userManager = userManager;
        _currentTenant = currentTenant;
    }

    public async Task<IReadOnlyList<TenantUserSummary>> ListUsersAsync(CancellationToken ct)
    {
        var tenantId = RequireTenantId();

        var users = _userManager.Users.Where(u => u.TenantId == tenantId).ToList();

        var summaries = new List<TenantUserSummary>();
        foreach (var user in users)
            summaries.Add(await ToSummaryAsync(user));

        return summaries;
    }

    public async Task<InviteUserResult> InviteUserAsync(string email, string displayName, string role, CancellationToken ct)
    {
        if (!TenantRoles.IsValid(role))
            throw new ArgumentException($"Rol inválido: {role}. Debe ser {string.Join(" o ", TenantRoles.All)}.", nameof(role));

        var tenantId = RequireTenantId();

        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
            throw new InvalidOperationException($"Ya existe un usuario con el email {email}.");

        var temporaryPassword = GenerateTemporaryPassword();

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            TenantId = tenantId,
            DisplayName = displayName,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, temporaryPassword);
        if (!createResult.Succeeded)
            throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, role);

        return new InviteUserResult(await ToSummaryAsync(user), temporaryPassword);
    }

    public async Task ChangeRoleAsync(Guid userId, string newRole, CancellationToken ct)
    {
        if (!TenantRoles.IsValid(newRole))
            throw new ArgumentException($"Rol inválido: {newRole}. Debe ser {string.Join(" o ", TenantRoles.All)}.", nameof(newRole));

        var user = await GetUserInCurrentTenantAsync(userId);

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Contains(TenantRoles.Owner) && newRole != TenantRoles.Owner)
            await EnsureNotLastOwnerAsync(user, "cambiarle el rol");

        if (currentRoles.Count > 0)
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

        await _userManager.AddToRoleAsync(user, newRole);
    }

    public async Task DeactivateUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await GetUserInCurrentTenantAsync(userId);

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(TenantRoles.Owner))
            await EnsureNotLastOwnerAsync(user, "desactivarlo");

        // Lockout en vez de borrar: el historial del usuario (quién validó
        // qué comprobante, por ejemplo) queda intacto, solo no puede
        // loguearse más. AuthService.LoginAsync ya chequea IsLockedOutAsync.
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
    }

    public async Task ReactivateUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await GetUserInCurrentTenantAsync(userId);
        await _userManager.SetLockoutEndDateAsync(user, null);
    }

    private async Task EnsureNotLastOwnerAsync(AppUser user, string action)
    {
        var ownersInTenant = _userManager.Users.Where(u => u.TenantId == user.TenantId).ToList();

        var activeOwnerCount = 0;
        foreach (var candidate in ownersInTenant)
        {
            var roles = await _userManager.GetRolesAsync(candidate);
            var isLockedOut = await _userManager.IsLockedOutAsync(candidate);
            if (roles.Contains(TenantRoles.Owner) && !isLockedOut) activeOwnerCount++;
        }

        // En este punto el usuario en cuestión todavía cuenta como Owner activo
        // (el caller no aplicó el cambio todavía), así que "1" significa que
        // ES el único — no se puede seguir.
        if (activeOwnerCount <= 1)
            throw new InvalidOperationException(
                $"No se puede {action}: es el único Owner activo del tenant. Asigná otro Owner primero.");
    }

    private async Task<AppUser> GetUserInCurrentTenantAsync(Guid userId)
    {
        var tenantId = RequireTenantId();

        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"Usuario {userId} no encontrado.");

        if (user.TenantId != tenantId)
            throw new InvalidOperationException($"El usuario {userId} pertenece a otro tenant.");

        return user;
    }

    private async Task<TenantUserSummary> ToSummaryAsync(AppUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var isLockedOut = await _userManager.IsLockedOutAsync(user);

        return new TenantUserSummary(
            user.Id, user.Email!, user.DisplayName,
            roles.FirstOrDefault() ?? TenantRoles.Staff,
            IsActive: !isLockedOut);
    }

    private Guid RequireTenantId()
        => _currentTenant.TenantId
           ?? throw new InvalidOperationException("No hay un tenant actual seteado en este scope.");

    private static string GenerateTemporaryPassword()
    {
        // Cumple la política de Identity configurada (mínimo 8, con mayúscula,
        // minúscula, dígito y símbolo) — se muestra una única vez en la
        // respuesta de InviteUserAsync.
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%";

        Span<char> chars = stackalloc char[12];
        chars[0] = Pick(upper);
        chars[1] = Pick(lower);
        chars[2] = Pick(digits);
        chars[3] = Pick(symbols);

        const string all = upper + lower + digits + symbols;
        for (var i = 4; i < chars.Length; i++)
            chars[i] = Pick(all);

        // Mezclar para que los primeros 4 caracteres no sean siempre
        // "Mayúscula, minúscula, dígito, símbolo" en ese orden fijo.
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);

        static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
    }
}
