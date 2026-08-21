using System.Security.Cryptography;
using System.Text;
using Hangfire.Dashboard;
using Microsoft.Extensions.Options;

namespace WhatsAppBot.Api.Security;

public class HangfireDashboardOptions
{
    public const string SectionName = "HangfireDashboard";
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
}

// El dashboard de Hangfire se navega desde el browser, no llama a la API
// con un Bearer token — por eso usa su propio mecanismo (Basic Auth con
// credenciales separadas del login normal), en vez de intentar reusar JWT.
public class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    private readonly HangfireDashboardOptions _options;

    public HangfireDashboardAuthFilter(IOptions<HangfireDashboardOptions> options)
    {
        _options = options.Value;
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        if (string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrWhiteSpace(_options.Password))
        {
            // Sin credenciales configuradas, preferimos no exponer el
            // dashboard en vez de dejarlo abierto por un olvido de configuración.
            return false;
        }

        var header = httpContext.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            Challenge(httpContext);
            return false;
        }

        try
        {
            var encoded = header["Basic ".Length..].Trim();
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var separatorIndex = decoded.IndexOf(':');
            if (separatorIndex < 0)
            {
                Challenge(httpContext);
                return false;
            }

            var username = decoded[..separatorIndex];
            var password = decoded[(separatorIndex + 1)..];

            var isValid = username == _options.Username && FixedTimeEquals(password, _options.Password);
            if (!isValid) Challenge(httpContext);
            return isValid;
        }
        catch (FormatException)
        {
            Challenge(httpContext);
            return false;
        }
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var bytesA = Encoding.UTF8.GetBytes(a);
        var bytesB = Encoding.UTF8.GetBytes(b);
        if (bytesA.Length != bytesB.Length) return false;
        return CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }

    private static void Challenge(HttpContext httpContext)
    {
        httpContext.Response.Headers.WWWAuthenticate = "Basic realm=\"Hangfire Dashboard\"";
        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
    }
}
