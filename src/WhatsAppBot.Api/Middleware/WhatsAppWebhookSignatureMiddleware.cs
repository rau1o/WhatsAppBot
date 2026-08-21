using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using WhatsAppBot.Infrastructure.WhatsApp;

namespace WhatsAppBot.Api.Middleware
{
    // Meta firma cada POST al webhook con HMAC-SHA256 usando el App Secret
    // (header "X-Hub-Signature-256: sha256=<hex>"). Sin esto, cualquiera que
    // adivine la URL del webhook puede mandar mensajes falsos como si vinieran
    // de un cliente real — pedidos falsos, comprobantes de pago falsos, etc.
    public class WhatsAppWebhookSignatureMiddleware
    {
        private const string SignatureHeader = "X-Hub-Signature-256";
        private const string SignaturePrefix = "sha256=";

        private readonly RequestDelegate _next;

        public WhatsAppWebhookSignatureMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IOptions<WhatsAppCloudApiOptions> options, ILogger<WhatsAppWebhookSignatureMiddleware> logger)
        {
            // El GET de verificación inicial de Meta no trae body firmado —
            // solo firmamos los POST con mensajes reales.
            var isWebhookPost = context.Request.Path.StartsWithSegments("/api/webhook/whatsapp")
                && HttpMethods.IsPost(context.Request.Method);

            if (!isWebhookPost)
            {
                await _next(context);
                return;
            }

            var appSecret = options.Value.AppSecret;
            if (string.IsNullOrWhiteSpace(appSecret))
            {
                // Sin AppSecret no hay nada que verificar. Esto es aceptable en
                // desarrollo local (donde simulamos el webhook a mano con curl/
                // Invoke-RestMethod), pero NUNCA debería pasar en producción —
                // por eso el warning explícito en cada request.
                logger.LogWarning(
                    "WhatsAppCloudApi:AppSecret no configurado — el webhook está aceptando requests SIN verificar firma. No usar así en producción.");
                await _next(context);
                return;
            }

            context.Request.EnableBuffering();

            string body;
            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync();
            }
            context.Request.Body.Position = 0; // el controller todavía necesita leer el body para el model binding

            var signatureHeader = context.Request.Headers[SignatureHeader].ToString();

            if (!IsValidSignature(body, signatureHeader, appSecret))
            {
                logger.LogWarning("Firma inválida en el webhook de WhatsApp — request rechazado.");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Firma inválida");
                return;
            }

            await _next(context);
        }

        private static bool IsValidSignature(string body, string signatureHeader, string appSecret)
        {
            if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith(SignaturePrefix))
                return false;

            var expectedHex = signatureHeader[SignaturePrefix.Length..];

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
            var computedHex = Convert.ToHexString(computedHash).ToLowerInvariant();

            if (computedHex.Length != expectedHex.Length) return false;

            // Comparación en tiempo constante — comparar con == filtraría por
            // timing en qué posición difieren los hashes.
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedHex), Encoding.UTF8.GetBytes(expectedHex));
        }
    }

}
