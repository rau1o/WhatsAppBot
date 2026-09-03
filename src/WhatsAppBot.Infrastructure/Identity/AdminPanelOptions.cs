namespace WhatsAppBot.Infrastructure.Identity;

public class AdminPanelOptions
{
    public const string SectionName = "AdminPanel";

    // Sin barra final — ej. "https://whatsappbotadminpanel-production.up.railway.app"
    public string PublicBaseUrl { get; set; } = "";
}
