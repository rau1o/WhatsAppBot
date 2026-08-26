using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
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

// El disco de Railway es efímero — sin esto, cada redeploy generaba claves
// de Data Protection nuevas, y con ellas cualquier sesión guardada vía
// ProtectedSessionStorage (ver Login.razor/MainLayout.razor) quedaba
// indescifrable: todos los usuarios logueados perdían su sesión en cada
// deploy, en silencio. Guardamos las claves en la misma base de Supabase
// para que sobrevivan el redeploy — la única excepción a "el panel no
// toca la base directamente" en todo este proyecto.
var dataProtectionConnectionString = builder.Configuration.GetConnectionString("DataProtection")
    ?? throw new InvalidOperationException("Falta la connection string 'DataProtection' en appsettings.json");

builder.Services.AddDbContext<DataProtectionKeysDbContext>(options =>
    options.UseNpgsql(dataProtectionConnectionString));

builder.Services.AddDataProtection()
    .SetApplicationName("WhatsAppBotAdminPanel")
    .PersistKeysToDbContext<DataProtectionKeysDbContext>();

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

// Tabla mínima(una sola, sin relación con nada del negocio).OJO:
// Database.EnsureCreated() NO sirve acá — esa función solo chequea si la
// base de datos EN SÍ existe, no si las tablas de este DbContext puntual
// existen. Como esta base ya existe (la usa el Api con sus propias
// migraciones), EnsureCreated() la veía "ya creada" y nunca llegaba a
// crear esta tabla. CREATE TABLE IF NOT EXISTS es idempotente y no
// necesita un segundo sistema de migraciones solo para esto.
using (var scope = app.Services.CreateScope())
{
    var dataProtectionDb = scope.ServiceProvider.GetRequiredService<DataProtectionKeysDbContext>();
    await dataProtectionDb.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "DataProtectionKeys" (
            "Id" serial PRIMARY KEY,
            "FriendlyName" text NULL,
            "Xml" text NULL
        );
        """);
}

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
