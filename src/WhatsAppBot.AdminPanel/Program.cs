using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using WhatsAppBot.AdminPanel.Components;
using WhatsAppBot.AdminPanel.Services;

var builder = WebApplication.CreateBuilder(args);
// Mismo motivo que en el Api: Railway termina HTTPS en su borde y reenvía
// HTTP puro para adentro — sin esto, UseHttpsRedirection()/UseHsts() pueden
// terminar en loop de redirección infinito, y las cookies "Secure" se
// rechazarían pensando que la conexión es HTTP.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Este esquema Cookie NUNCA se usa para iniciar sesión de verdad (jamás
// llamamos SignInAsync) — existe solo para que ASP.NET Core sepa a dónde
// mandar la PRIMERA carga HTTP completa de una página [Authorize] cuando
// todavía no hay circuito de Blazor ni AuthState. El login real es el JWT
// que maneja AuthState/ApiAuthStateProvider, scoped al circuito.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
    });

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<AuthenticationStateProvider, ApiAuthStateProvider>();

var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Falta 'Api:BaseUrl' en appsettings.json");

builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});
// Nominatim pide identificar la app en el User-Agent — sin esto, algunos
// requests se rechazan silenciosamente según su política de uso.
builder.Services.AddHttpClient("Nominatim", client =>
{
    client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WhatsAppBotAdminPanel/1.0 (contacto@tutienda.com)");
});
builder.Services.AddScoped<GeocodingService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
