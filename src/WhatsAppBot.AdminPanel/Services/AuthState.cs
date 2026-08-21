namespace WhatsAppBot.AdminPanel.Services;

// Scoped: una instancia por circuito de Blazor Server (por pestaña del
// browser conectada). El JWT vive acá, en memoria del servidor — nunca se
// manda al cliente como cookie ni localStorage. Se pierde si el circuito
// se cae (ej. refrescar la página), lo cual implica re-loguearse; es una
// limitación aceptada para este alcance, igual que la falta de refresh
// token del lado del Api.
public class AuthState
{
    public string? Token { get; private set; }
    public string? Email { get; private set; }
    public string? Role { get; private set; }
    public Guid? TenantId { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }

    public bool IsAuthenticated => Token is not null && ExpiresAtUtc > DateTime.UtcNow;

    public event Action? OnChange;

    public void SetSession(string token, string email, string role, Guid tenantId, DateTime expiresAtUtc)
    {
        Token = token;
        Email = email;
        Role = role;
        TenantId = tenantId;
        ExpiresAtUtc = expiresAtUtc;
        OnChange?.Invoke();
    }

    public void Clear()
    {
        Token = null;
        Email = null;
        Role = null;
        TenantId = null;
        ExpiresAtUtc = null;
        OnChange?.Invoke();
    }
}
