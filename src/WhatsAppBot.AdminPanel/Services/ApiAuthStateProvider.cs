using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace WhatsAppBot.AdminPanel.Services;

public class ApiAuthStateProvider : AuthenticationStateProvider
{
    private readonly AuthState _authState;

    public ApiAuthStateProvider(AuthState authState)
    {
        _authState = authState;
        _authState.OnChange += () => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_authState.IsAuthenticated)
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, _authState.Email!),
            new(ClaimTypes.Role, _authState.Role!),
            new("tenant_id", _authState.TenantId!.Value.ToString())
        };

        var identity = new ClaimsIdentity(claims, authenticationType: "ApiJwt");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }
}
