using System.Net.Http.Headers;
using System.Net.Http.Json;
using WhatsAppBot.AdminPanel.Models;

namespace WhatsAppBot.AdminPanel.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    private readonly AuthState _authState;

    public ApiClient(HttpClient http, AuthState authState)
    {
        _http = http;
        _authState = authState;
    }

    public async Task<(bool Success, string? Error)> LoginAsync(string email, string password)
    {
        HttpResponseMessage response;

        try
        {
            response = await _http.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password));
        }
        catch (HttpRequestException ex)
        {
            // Típicamente: el Api no está corriendo, está en otro puerto, o el
            // certificado HTTPS de desarrollo no está confiado en esta máquina
            // (dotnet dev-certs https --trust). Sin este catch, esta excepción
            // tumba todo el circuito de Blazor en vez de mostrarse en el form.
            return (false, $"No pudimos conectar con el servidor ({ex.Message}). ¿Está corriendo el Api?");
        }
        catch (TaskCanceledException)
        {
            return (false, "El servidor tardó demasiado en responder. Probá de nuevo.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            return (false, "Demasiados intentos. Esperá un minuto y probá de nuevo.");

        if (!response.IsSuccessStatusCode)
            return (false, "Email o contraseña incorrectos.");

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (result is null) return (false, "Respuesta inesperada del servidor.");

        _authState.SetSession(result.Token, email, result.Role, result.TenantId, result.ExpiresAtUtc);
        return (true, null);
    }

    public async Task<List<ProductDto>> GetProductsAsync()
    {
        var response = await SendAsync(HttpMethod.Get, "api/products");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ProductDto>>() ?? [];
    }

    public async Task<ProductDto?> CreateProductAsync(UpsertProductRequest request)
    {
        var response = await SendAsync(HttpMethod.Post, "api/products", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ProductDto>() : null;
    }

    public async Task<ProductDto?> UpdateProductAsync(Guid id, UpsertProductRequest request)
    {
        var response = await SendAsync(HttpMethod.Put, $"api/products/{id}", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ProductDto>() : null;
    }

    public async Task<bool> DeleteProductAsync(Guid id)
    {
        var response = await SendAsync(HttpMethod.Delete, $"api/products/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<ConversationSummaryDto>> GetConversationsAsync()
    {
        var response = await SendAsync(HttpMethod.Get, "api/conversations");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ConversationSummaryDto>>() ?? [];
    }

    public async Task<List<UserDto>> GetUsersAsync()
    {
        var response = await SendAsync(HttpMethod.Get, "api/users");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<UserDto>>() ?? [];
    }

    public async Task<(InviteUserResponse? Result, string? Error)> InviteUserAsync(InviteUserRequest request)
    {
        var response = await SendAsync(HttpMethod.Post, "api/users", request);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<InviteUserResponse>(), null);

        return (null, await TryReadErrorMessage(response) ?? "No pudimos invitar al usuario.");
    }

    public async Task<(bool Success, string? Error)> ChangeRoleAsync(Guid userId, string role)
    {
        var response = await SendAsync(HttpMethod.Put, $"api/users/{userId}/role", new ChangeRoleRequest(role));
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await TryReadErrorMessage(response) ?? "No pudimos cambiar el rol.");
    }

    public async Task<(bool Success, string? Error)> DeactivateUserAsync(Guid userId)
    {
        var response = await SendAsync(HttpMethod.Post, $"api/users/{userId}/deactivate");
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await TryReadErrorMessage(response) ?? "No pudimos desactivar al usuario.");
    }

    public async Task<bool> ReactivateUserAsync(Guid userId)
    {
        var response = await SendAsync(HttpMethod.Post, $"api/users/{userId}/reactivate");
        return response.IsSuccessStatusCode;
    }

    public async Task<TenantSettingsDto?> GetTenantSettingsAsync()
    {
        var response = await SendAsync(HttpMethod.Get, "api/tenant-settings");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<TenantSettingsDto>() : null;
    }

    public async Task<(TenantSettingsDto? Result, string? Error)> UpdateTenantSettingsAsync(UpdateTenantSettingsRequest request)
    {
        var response = await SendAsync(HttpMethod.Put, "api/tenant-settings", request);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<TenantSettingsDto>(), null);

        return (null, await TryReadErrorMessage(response) ?? "No pudimos guardar los cambios.");
    }

    public async Task<(TenantSettingsDto? Result, string? Error)> UploadTenantImageAsync(
        string slot, Stream fileStream, string fileName, string contentType)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, $"api/tenant-settings/{slot}") { Content = content };
        if (_authState.Token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authState.Token);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (null, $"No pudimos conectar con el servidor ({ex.Message}).");
        }

        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<TenantSettingsDto>(), null);

        return (null, await TryReadErrorMessage(response) ?? "No pudimos subir la imagen.");
    }

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var response = await SendAsync(HttpMethod.Post, "api/auth/change-password",
            new ChangePasswordRequest(currentPassword, newPassword));

        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await TryReadErrorMessage(response) ?? "No pudimos cambiar la contraseña.");
    }

    public async Task<List<PaymentProofDto>> GetPendingPaymentProofsAsync()
    {
        var response = await SendAsync(HttpMethod.Get, "api/payment-proofs/pending");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<PaymentProofDto>>() ?? [];
    }

    public async Task<(bool Success, string? Error)> ApprovePaymentProofAsync(Guid id)
    {
        var response = await SendAsync(HttpMethod.Post, $"api/payment-proofs/{id}/approve");
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await TryReadErrorMessage(response) ?? "No pudimos aprobar el comprobante.");
    }

    public async Task<(bool Success, string? Error)> RejectPaymentProofAsync(Guid id)
    {
        var response = await SendAsync(HttpMethod.Post, $"api/payment-proofs/{id}/reject");
        if (response.IsSuccessStatusCode) return (true, null);
        return (false, await TryReadErrorMessage(response) ?? "No pudimos rechazar el comprobante.");
    }

    private static async Task<string?> TryReadErrorMessage(HttpResponseMessage response)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return payload is not null && payload.TryGetValue("message", out var msg) ? msg : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);

        if (_authState.Token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authState.Token);

        if (body is not null)
            request.Content = JsonContent.Create(body);

        try
        {
            return await _http.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Mismo motivo que en LoginAsync: una falla de red acá no debe
            // tumbar el circuito de Blazor. Devolvemos una respuesta sintética
            // no exitosa — los callers ya chequean IsSuccessStatusCode.
            return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
            {
                ReasonPhrase = ex.Message
            };
        }
    }
}
