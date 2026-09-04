using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace WhatsAppBot.AdminPanel.Services;

// Guarda SOLO el refresh token — nada más hace falta persistir. El JWT, el
// rol, y el tenant se reconstruyen llamando a /api/auth/refresh con este
// token (ver ApiClient.RestoreSessionAsync). Usa sessionStorage (no
// localStorage): sobrevive un F5, pero se borra solo si cerrás la pestaña —
// un default razonable para un panel admin, ni tan efímero como la memoria
// del circuito, ni tan persistente como para sobrevivir semanas en un
// browser compartido.
public class SessionPersistence
{
    private const string StorageKey = "wb_refresh_token";

    private readonly ProtectedSessionStorage _storage;

    public SessionPersistence(ProtectedSessionStorage storage)
    {
        _storage = storage;
    }

    public async Task<string?> LoadRefreshTokenAsync()
    {
        try
        {
            var result = await _storage.GetAsync<string>(StorageKey);
            return result.Success ? result.Value : null;
        }
        catch (InvalidOperationException)
        {
            // El JS interop todavía no está disponible (ej. durante un
            // prerender) — tratamos esto como "no hay sesión guardada" en
            // vez de romper la carga de la página.
            return null;
        }
    }

    public async Task SaveRefreshTokenAsync(string refreshToken)
    {
        try
        {
            await _storage.SetAsync(StorageKey, refreshToken);
        }
        catch (InvalidOperationException)
        {
            // Mismo motivo — no debería pasar en este punto del ciclo de
            // vida, pero si pasa, no vale la pena tumbar el circuito por esto.
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            await _storage.DeleteAsync(StorageKey);
        }
        catch (InvalidOperationException) { }
    }
}
