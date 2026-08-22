namespace WhatsAppBot.Infrastructure.Storage;

public class R2FileStorageOptions
{
    public const string SectionName = "R2Storage";

    public string AccountId { get; set; } = default!;
    public string AccessKeyId { get; set; } = default!;
    public string SecretAccessKey { get; set; } = default!;
    public string BucketName { get; set; } = default!;

    // El dominio público desde donde se sirven los archivos — el subdominio
    // gratis *.r2.dev que da Cloudflare, o un dominio propio si conectaste uno.
    public string PublicBaseUrl { get; set; } = default!;
}
