using Microsoft.Extensions.Options;
using WhatsAppBot.Application.Abstractions;

namespace WhatsAppBot.Infrastructure.Storage;

// TODO: reemplazar por AzureBlobFileStorage o S3FileStorage antes de producción.
// Guardar comprobantes de pago en el disco del servidor no sobrevive un
// redeploy ni escala a más de una instancia — sirve para desarrollar y
// probar el flujo, no para el negocio real.
public class LocalFileStorage : IFileStorage
{
    private readonly FileStorageOptions _options;

    public LocalFileStorage(IOptions<FileStorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> SaveAsync(byte[] content, string fileName, string contentType, CancellationToken ct)
    {
        Directory.CreateDirectory(_options.LocalPath);

        var safeFileName = Path.GetFileName(fileName); // evita path traversal si el nombre viene de afuera
        var fullPath = Path.Combine(_options.LocalPath, safeFileName);

        await File.WriteAllBytesAsync(fullPath, content, ct);

        return $"{_options.PublicBaseUrl.TrimEnd('/')}/{safeFileName}";
    }
}
