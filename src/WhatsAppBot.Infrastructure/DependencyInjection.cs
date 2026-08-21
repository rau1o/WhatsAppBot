using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Infrastructure.BackgroundJobs;
using WhatsAppBot.Infrastructure.Identity;
using WhatsAppBot.Infrastructure.MultiTenacy;
using WhatsAppBot.Infrastructure.Persistence;
using WhatsAppBot.Infrastructure.Persistence.Repositories;
using WhatsAppBot.Infrastructure.Storage;
using WhatsAppBot.Infrastructure.WhatsApp;

namespace WhatsAppBot.Infrastructure;


public static class DependencyInjection
    {
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<WhatsAppCloudApiOptions>(config.GetSection(WhatsAppCloudApiOptions.SectionName));

        // Sin AccessToken configurado, usamos un sender que solo loguea —
        // así se puede debuggear el flujo completo de conversación sin
        // credenciales reales de Meta.
        var whatsAppAccessToken = config[$"{WhatsAppCloudApiOptions.SectionName}:AccessToken"];
        if (string.IsNullOrWhiteSpace(whatsAppAccessToken))
        {
            services.AddScoped<IWhatsAppMessageSender, LoggingWhatsAppMessageSender>();
            
        }
        else
        {
            services.AddHttpClient<IWhatsAppMessageSender, WhatsAppCloudApiSender>();
           
        }

        services.Configure<FileStorageOptions>(config.GetSection(FileStorageOptions.SectionName));
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        // Scoped: una instancia por request HTTP o por ejecución de job de
        // Hangfire — nunca se comparte entre tenants distintos.
        services.AddScoped<ICurrentTenantAccessor, CurrentTenantAccessor>();

        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta la connection string 'Default' en appsettings.json");

        services.AddDbContext<WhatsAppBotDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<ITenantRepository, EfTenantRepository>();
        services.AddScoped<IConversationRepository, EfConversationRepository>();
        services.AddScoped<IProductRepository, EfProductRepository>();
        services.AddScoped<IOrderRepository, EfOrderRepository>();
        services.AddScoped<IPaymentProofRepository, EfPaymentProofRepository>();

        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(opt => opt.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer();
        services.AddScoped<IBackgroundJobEnqueuer, HangfireBackgroundJobEnqueuer>();

        // --- Identity: solo lo usamos como store de usuarios/contraseñas.
        // AddIdentityCore (no AddIdentity completo) porque no queremos
        // cookies, ni Razor Pages de login, ni todo el andamiaje pensado
        // para MVC — la autenticación real de la API es por JWT.
        services.AddIdentityCore<AppUser>(options =>
        {
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
        })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<WhatsAppBotDbContext>();

        // El default de Identity son 100.000 iteraciones de PBKDF2-HMAC-SHA256
        // — quedó corto frente a la recomendación actual de OWASP (600.000,
        // dado lo rápido que un GPU moderno prueba hashes). Subirlo es gratis:
        // no requiere migración, Identity re-hashea sola la contraseña de cada
        // usuario la próxima vez que hace login exitoso.
        services.Configure<PasswordHasherOptions>(options =>
        {
            options.IterationCount = 600_000;
        });

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.AddSingleton<JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserManagementService, UserManagementService>();

        var jwtOptions = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Falta la sección 'Jwt' en appsettings.json");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Sin esto, ASP.NET Core remapea "sub" a ClaimTypes.NameIdentifier
                // (un URI legado) al armar el ClaimsPrincipal — entonces
                // User.FindFirst(JwtRegisteredClaimNames.Sub) no lo encuentra,
                // aunque el token sí lo tenga. Con esto, los claims quedan
                // exactamente como los emitimos en JwtTokenService.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
                };
            });

        services.AddAuthorization();

        return services;
    }
}

