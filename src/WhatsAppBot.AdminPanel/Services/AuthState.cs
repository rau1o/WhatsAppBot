namespace WhatsAppBot.AdminPanel.Services;

// Scoped: una instancia por circuito de Blazor Server (por pestaña del
// browser conectada). El JWT vive acá, en memoria del servidor — nunca se
// manda al cliente como cookie ni localStorage. Se pierde si el circuito
// se cae (ej. refrescar la página), lo cual implica re-loguearse; es una
// limitación aceptada para este alcance, El refresh token SÍ evita tener
// que volver a loguearse solo por vencimiento del JWT mientras el circuito
// siga vivo — ver ApiClient.EnsureFreshTokenAsync.

public class AuthState
{
    public string? Token { get; private set; }
    public string? RefreshToken { get; private set; }
    public string? Email { get; private set; }
    public string? Role { get; private set; }
    public Guid? TenantId { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }

    public bool IsAuthenticated => Token is not null && ExpiresAtUtc > DateTime.UtcNow;

    public event Action? OnChange;

    public void SetSession(string token, string refreshToken, string email, string role, Guid tenantId, DateTime expiresAtUtc)
    {
        Token = token;
        Email = email;
        RefreshToken = refreshToken;
        Role = role;
        TenantId = tenantId;
        ExpiresAtUtc = expiresAtUtc;
        OnChange?.Invoke();
    }

    public void Clear()
    {
        Token = null;
        RefreshToken = null;
        Email = null;
        Role = null;
        TenantId = null;
        ExpiresAtUtc = null;
        OnChange?.Invoke();
    }
}
