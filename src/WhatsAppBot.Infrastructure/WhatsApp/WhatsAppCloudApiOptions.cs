namespace WhatsAppBot.Infrastructure.WhatsApp;

public class WhatsAppCloudApiOptions
{
    public const string SectionName = "WhatsAppCloudApi";

    public string BaseUrl { get; set; } = "https://graph.facebook.com/v20.0";
    public string AccessToken { get; set; } = default!; // token de sistema del Business Manager
    public string VerifyToken { get; set; } = default!; // para el GET de verificación del webhook

    // Usado para verificar la firma X-Hub-Signature-256 de cada request
    // entrante — NO es el mismo valor que AccessToken. Se consigue en
    // Meta App Dashboard → Configuración básica → Clave secreta de la app.
    public string? AppSecret { get; set; }
}
