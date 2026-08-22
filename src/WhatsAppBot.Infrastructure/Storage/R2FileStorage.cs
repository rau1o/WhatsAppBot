using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using WhatsAppBot.Application.Abstractions;

namespace WhatsAppBot.Infrastructure.Storage;

// R2 habla el mismo protocolo que S3 — reusamos el SDK de AWS apuntado al
// endpoint de Cloudflare, en vez de necesitar un SDK propio. La diferencia
// real está en el costo: R2 no cobra nada de egress (bajar los archivos es
// gratis), que es justo el costo que más se acumula con fotos que la gente
// ve seguido desde el panel.
public class R2FileStorage : IFileStorage
{
    private readonly R2FileStorageOptions _options;
    private readonly AmazonS3Client _client;

    public R2FileStorage(IOptions<R2FileStorageOptions> options)
    {
        _options = options.Value;

        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{_options.AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true
        };

        _client = new AmazonS3Client(_options.AccessKeyId, _options.SecretAccessKey, config);
    }

    public async Task<string> SaveAsync(byte[] content, string fileName, string contentType, CancellationToken ct)
    {
        var safeFileName = Path.GetFileName(fileName); // evita path traversal, mismo criterio que LocalFileStorage

        using var stream = new MemoryStream(content);

        // Mismo nombre = mismo Key = la subida nueva pisa la anterior en el
        // bucket, igual que ya hacía LocalFileStorage — así no se acumulan
        // fotos viejas de fachada/QR cada vez que el dueño actualiza una.
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = safeFileName,
            InputStream = stream,
            ContentType = contentType
        };

        await _client.PutObjectAsync(request, ct);

        return $"{_options.PublicBaseUrl.TrimEnd('/')}/{safeFileName}";
    }
}
