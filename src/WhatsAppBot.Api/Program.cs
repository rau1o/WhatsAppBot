using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using WhatsAppBot.Api.Middleware;
using WhatsAppBot.Api.Security;
using WhatsAppBot.Application;
using WhatsAppBot.Infrastructure;
using WhatsAppBot.Infrastructure.Identity;
using WhatsAppBot.Infrastructure.Persistence;
using WhatsAppBot.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// Railway (como cualquier PaaS con proxy inverso) termina el HTTPS en su
// borde y reenvía HTTP puro para adentro. Sin esto, la app no se entera de
// que la request original sí era HTTPS, y UseHttpsRedirection()/UseHsts()
// pueden terminar en loop de redirección infinito.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Railway no publica una lista fija de IPs de proxy — confiamos en el
    // header tal cual llega desde su borde en vez de whitelisting por IP.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllers();

// El Api solo conoce estos dos métodos de extensión — no sabe
// (ni le importa) qué handlers o adaptadores hay adentro de cada capa.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<HangfireDashboardOptions>(builder.Configuration.GetSection(HangfireDashboardOptions.SectionName));
// Manejo global de errores: cualquier excepción no capturada explícitamente
// en un controller termina acá, en vez de devolver un stack trace crudo o
// tumbar el proceso.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Health check simple: confirma que el proceso está vivo Y que puede
// hablar con la base — útil para Railway/monitoreo externo (UptimeRobot,
// etc.). No requiere autenticación a propósito, como cualquier endpoint
// de salud pensado para que lo pegue un load balancer o un monitor externo.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<WhatsAppBotDbContext>("database");
// Frena fuerza bruta contra /api/auth/login: 5 intentos por minuto por IP,
// sin cola — el sexto intento se rechaza directo con 429 en vez de esperar.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Límite general para CUALQUIER endpoint, por IP — protección básica
    // contra abuso/DoS que no depende de que cada controller se acuerde de
    // pedirlo. Los límites más estrictos (como el de login) se suman
    // ENCIMA de este, no lo reemplazan.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Frena fuerza bruta contra /api/auth/login: 5 intentos por minuto por
    // IP, sin cola — el sexto intento se rechaza directo con 429.
    options.AddFixedWindowLimiter("login", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});


// Sin orígenes configurados, no se permite ningún cross-origin — preferible
// a un default permisivo. Agregá el dominio real del panel admin en
// Cors:AllowedOrigins apenas lo tengas.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminPanel", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    // Conveniencia solo para desarrollo local. En producción las migraciones
    // se aplican como paso explícito del pipeline de CI/CD, no al arrancar.
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<WhatsAppBotDbContext>();
        db.Database.Migrate();
    }

    await DevSeeder.SeedAsync(app.Services);
}
else
{
    // HSTS le dice al browser "acordate de usar HTTPS acá" por un tiempo —
    // no tiene sentido en desarrollo local con certificados self-signed.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseCors("AdminPanel");

// Verifica la firma de Meta antes que nada — el webhook no usa JWT,
// usa su propio mecanismo de confianza (HMAC con el App Secret).
app.UseMiddleware<WhatsAppWebhookSignatureMiddleware>();


// El orden importa: primero quién sos (autenticación), después qué tenant
// te corresponde (nuestro middleware), recién ahí qué podés hacer (autorización).
app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();

app.UseRateLimiter();

// Sirve las fotos de comprobantes de pago guardadas por LocalFileStorage.
// TODO: cuando se reemplace por Azure Blob/S3, esto ya no hace falta —
// las URLs van a apuntar directo al storage en la nube.
var fileStorageOptions = app.Services.GetRequiredService<IOptions<FileStorageOptions>>().Value;
Directory.CreateDirectory(fileStorageOptions.LocalPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.GetFullPath(fileStorageOptions.LocalPath)),
    RequestPath = fileStorageOptions.PublicBaseUrl
});

app.MapControllers();

// Basic Auth con credenciales propias (HangfireDashboard:Username/Password),
// separadas del login normal — ver HangfireDashboardAuthFilter.
var hangfireDashboardOptions = app.Services.GetRequiredService<IOptions<HangfireDashboardOptions>>();
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new IDashboardAuthorizationFilter[] { new HangfireDashboardAuthFilter(hangfireDashboardOptions) }
});

app.Run();
